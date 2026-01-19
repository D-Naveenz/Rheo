using Rheo.Storage.Information;
using System.Diagnostics;

namespace Rheo.Storage.Core
{
    public abstract partial class DirectoryHandler
    {
        /// <summary>
        /// Asynchronously copies the contents of the current directory to the specified destination directory,
        /// including all subdirectories and files.
        /// </summary>
        /// <remarks>All subdirectories and files are copied recursively. If the operation is canceled,
        /// any partially copied data in the destination directory is deleted before the exception is thrown. The method
        /// reports aggregate progress for the entire directory copy if a progress reporter is provided.</remarks>
        /// <param name="destination">The path to the destination directory where the contents will be copied. If the directory does not exist, it
        /// will be created.</param>
        /// <param name="overwrite">true to overwrite existing files in the destination directory; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the total bytes transferred during the copy
        /// operation. May be null if progress reporting is not required.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a DirectoryInformation
        /// object for the destination directory after the copy completes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to an error accessing the source or destination directory.</exception>
        internal async Task<DirectoryInformation> CopyInternalAsync(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);
            var fullPath = FullPath;
            var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
            var totalBytes = Information!.Size;
            long bytesTransferred = 0;
            var stopwatch = Stopwatch.StartNew();

            // OPERATION
            try
            {
                await WaitForDirectoryAvailableAsync(fullPath, cancellationToken: cancellationToken);   // Ensure source directory is accessible

                // Step 1: Create all directories first (including empty ones)
                var directories = Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories);
                foreach (var dir in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var relativeDir = Path.GetRelativePath(fullPath, dir);
                    var targetDir = Path.Combine(destination, relativeDir);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }

                // Step 2: Copy all files using FileHandling infrastructure
                var copyTasks = files.Select(async filePath =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Calculate relative path and destination
                    var relativePath = Path.GetRelativePath(fullPath, filePath);
                    var targetFilePath = Path.Combine(destination, relativePath);

                    // Create FileObject for source file
                    using var fileObj = new FileObject(filePath);
                    
                    // Create a progress wrapper to aggregate progress
                    IProgress<StorageProgress>? fileProgress = null;
                    if (progress != null)
                    {
                        fileProgress = new Progress<StorageProgress>(sp =>
                        {
                            // Update overall progress
                            var currentTotal = Interlocked.Add(ref bytesTransferred, sp.BytesTransferred);
                            
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double bytesPerSecond = elapsedSeconds > 0 ? currentTotal / elapsedSeconds : 0;
                            
                            progress.Report(new StorageProgress
                            {
                                TotalBytes = totalBytes,
                                BytesTransferred = currentTotal,
                                BytesPerSecond = bytesPerSecond
                            });
                        });
                    }

                    // Use FileHandling.CopyAsync for efficient file copying
                    var targetDir = Path.GetDirectoryName(targetFilePath)!;
                    await fileObj.CopyInternalAsync(
                        targetDir,
                        overwrite,
                        fileProgress,
                        cancellationToken);
                }).ToList();

                // Wait for all file copies to complete
                await Task.WhenAll(copyTasks);
                
                // Honor cancellation even if all copies completed successfully
                // This ensures we don't return success if cancellation was requested during the operation
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // Clean up partial copy on cancellation
                try
                {
                    if (Directory.Exists(destination))
                    {
                        Directory.Delete(destination, true);
                    }
                }
                catch { /* Best effort cleanup */ }
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to copy directory from '{fullPath}' to '{destination}'.", ex);
            }

            // FINALIZATION - Return directory information
            return new DirectoryInformation(destination);
        }

        /// <summary>
        /// Asynchronously deletes the directory and all its contents at the specified path.
        /// </summary>
        /// <remarks>If the directory does not exist, the method completes successfully and raises the
        /// deletion event. The method raises a change notification after the directory is deleted.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be deleted due to an I/O error or insufficient permissions.</exception>
        internal async Task DeleteInternalAsync(CancellationToken cancellationToken = default)
        {
            var _lock = Semaphore;
            var path = FullPath; // Store path before raising event

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WaitForDirectoryAvailableAsync(path, cancellationToken: cancellationToken);   // Ensure source directory is accessible

                await Task.Run(() => Directory.Delete(path, true), cancellationToken);

                // Raise the Changed event to notify deletion
                RaiseChanged(StorageChangeType.Deleted, null);
            }
            catch (DirectoryNotFoundException)
            {
                // Directory already deleted - still raise the event
                RaiseChanged(StorageChangeType.Deleted, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to delete directory at '{FullPath}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Moves the current directory to the specified destination path asynchronously, optionally overwriting an
        /// existing directory.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. For cross-volume moves, the directory is copied to the destination and then deleted
        /// from the source. In the event of a failure during a cross-volume move, the method attempts to roll back any
        /// partially completed operations. Progress reporting is only available for cross-volume moves, as same-volume
        /// moves are typically instantaneous.</remarks>
        /// <param name="destination">The full path to the destination directory. Must be a valid path and cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination directory if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. Progress is only reported for
        /// cross-volume moves.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the move operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a DirectoryInformation object
        /// for the moved directory at the destination path.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to an I/O error or insufficient permissions.</exception>
        internal async Task<DirectoryInformation> MoveInternalAsync(
            string destination, 
            bool overwrite, 
            IProgress<StorageProgress>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);
            var _lock = Semaphore;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WaitForDirectoryAvailableAsync(FullPath, cancellationToken: cancellationToken);   // Ensure source directory is accessible

                if (IsInTheSameRoot(destination))
                {
                    // Handle overwrite for same-volume moves
                    if (overwrite && Directory.Exists(destination))
                    {
                        Directory.Delete(destination, true);
                    }
                    
                    // Same volume move - fast operation (just directory entry update)
                    await Task.Run(() => Directory.Move(FullPath, destination), cancellationToken);

                    // Note: No detailed progress for same-volume moves (typically instantaneous)
                    progress?.Report(new StorageProgress
                    {
                        TotalBytes = 1,
                        BytesTransferred = 1,
                        BytesPerSecond = 0
                    });

                    // FINALIZATION - Create and return new information
                    var newInfo = new DirectoryInformation(destination);
                    RaiseChanged(StorageChangeType.Relocated, newInfo);
                    return newInfo;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to move directory from '{FullPath}' to '{destination}'.", ex);
            }
            finally
            {
                _lock.Release();
            }

            // Cross-volume move - lock released, now perform copy + delete
            DirectoryInformation? copiedInfo = null;
            try
            {
                copiedInfo = await CopyInternalAsync(destination, overwrite, progress, cancellationToken);
                await DeleteInternalAsync(cancellationToken);

                // Raise relocation event for the source object
                RaiseChanged(StorageChangeType.Relocated, copiedInfo);

                // FINALIZATION
                return copiedInfo;
            }
            catch
            {
                // Rollback: Attempt to delete the copied directory if it was created
                if (copiedInfo != null)
                {
                    try
                    {
                        var tempObj = new DirectoryObject(copiedInfo.AbsolutePath);
                        await tempObj.DeleteInternalAsync(cancellationToken);
                    }
                    catch
                    {
                        // Suppress exceptions during rollback to avoid masking the original error
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the current directory to the specified new name asynchronously and returns information about the
        /// renamed directory.
        /// </summary>
        /// <remarks>The operation is performed atomically and raises a change event upon successful
        /// completion. If the operation is canceled, the directory remains unchanged.</remarks>
        /// <param name="newName">The new name for the directory. Must be a valid directory name and cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a DirectoryInformation object
        /// for the renamed directory.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be renamed, such as when the destination already exists or access is denied.</exception>
        internal async Task<DirectoryInformation> RenameInternalAsync(
            string newName,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ThrowIfInvalidDirectoryName(newName);
            var destination = ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);
            var _lock = Semaphore;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WaitForDirectoryAvailableAsync(FullPath, cancellationToken: cancellationToken);   // Ensure source directory is accessible

                await Task.Run(() => Directory.Move(FullPath, destination), cancellationToken);

                // FINALIZATION - Create new information and raise event
                var newInfo = new DirectoryInformation(destination);
                RaiseChanged(StorageChangeType.Relocated, newInfo);
                return newInfo;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to rename directory from '{FullPath}' to '{destination}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
