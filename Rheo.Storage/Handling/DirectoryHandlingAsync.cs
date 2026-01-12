using System.Diagnostics;

namespace Rheo.Storage.Handling
{
    internal static partial class DirectoryHandling
    {
        /// <summary>
        /// Asynchronously copies the entire directory and its contents to a new location, optionally overwriting
        /// existing files and reporting progress.
        /// </summary>
        /// <remarks>This method copies all files and subdirectories recursively. It leverages the existing
        /// file copy infrastructure for efficient, double-buffered I/O. Empty directories are preserved. Progress is
        /// aggregated across all file copy operations.</remarks>
        /// <param name="source">The source DirectoryObject representing the directory to copy. Must not be null.</param>
        /// <param name="destination">The destination path where the directory will be copied. If not absolute, it will be resolved.</param>
        /// <param name="overwrite">true to overwrite existing files; otherwise, false. If false and a file exists, an exception is thrown.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the overall copy operation, including total bytes
        /// and transfer rate. May be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a DirectoryObject
        /// representing the newly created directory at the destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to I/O errors or insufficient permissions.</exception>
        public static async Task<DirectoryObject> CopyAsync(
            DirectoryObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var fullPath = source.FullPath;
            var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
            var totalBytes = source.Information!.Size;
            long bytesTransferred = 0;
            var stopwatch = Stopwatch.StartNew();

            // OPERATION
            try
            {
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
                    await FileHandling.CopyAsync(
                        fileObj,
                        targetDir,
                        overwrite,
                        fileProgress,
                        cancellationToken);
                }).ToList();

                // Wait for all file copies to complete
                await Task.WhenAll(copyTasks);
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

            // FINALIZATION
            return new DirectoryObject(destination);
        }

        /// <summary>
        /// Asynchronously deletes the specified directory and all its contents from the file system.
        /// </summary>
        /// <remarks>This method deletes the directory recursively, including all files and subdirectories.
        /// After successful deletion, the DirectoryObject is disposed and should not be used for further
        /// operations. The method acquires a lock on the DirectoryObject to ensure thread safety during the delete
        /// operation.</remarks>
        /// <param name="source">The DirectoryObject representing the directory to delete. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be deleted due to an I/O error or insufficient permissions.</exception>
        public static async Task DeleteAsync(DirectoryObject source, CancellationToken cancellationToken = default)
        {
            var _lock = source.Semaphore;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var path = source.FullPath; // Store path before disposing

                // Dispose the source object to release resources
                source.Dispose();
                await Task.Run(() => Directory.Delete(path, true), cancellationToken);
            }
            catch (DirectoryNotFoundException)
            {
                // Directory already deleted; treat as successful deletion
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)  // ✅ Specific exception filter
            {
                throw new InvalidOperationException($"Failed to delete directory at '{source.FullPath}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Asynchronously moves the specified directory to a new destination, optionally overwriting an existing directory.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. If they are on different volumes, the directory is copied and then the source is
        /// deleted. The source directory is disposed after a successful move. Progress updates are reported during
        /// cross-volume moves.</remarks>
        /// <param name="source">The directory to move. Must not be null and must refer to an existing directory.</param>
        /// <param name="destination">The full path to the destination. Cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination directory if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. May be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the move operation.</param>
        /// <returns>A task that represents the asynchronous move operation. The task result contains a DirectoryObject
        /// representing the directory at the new destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to I/O errors or insufficient permissions.</exception>
        public static async Task<DirectoryObject> MoveAsync(
            DirectoryObject source, 
            string destination, 
            bool overwrite, 
            IProgress<StorageProgress>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var _lock = source.Semaphore;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (source.IsInTheSameRoot(destination))
                {
                    // ✅ FIX: Handle overwrite for same-volume moves
                    if (overwrite && Directory.Exists(destination))
                    {
                        Directory.Delete(destination, true);
                    }
                    
                    // Same volume move - fast operation (just directory entry update)
                    await Task.Run(() => Directory.Move(source.FullPath, destination), cancellationToken);

                    // Dispose the source object to release resources
                    source.Dispose();

                    // Note: No detailed progress for same-volume moves (typically instantaneous)
                    progress?.Report(new StorageProgress
                    {
                        TotalBytes = 1,
                        BytesTransferred = 1,
                        BytesPerSecond = 0
                    });

                    // FINALIZATION
                    return new DirectoryObject(destination);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)  // ✅ FIX: Specific exception filter
            {
                throw new InvalidOperationException($"Failed to move directory from '{source.FullPath}' to '{destination}'.", ex);
            }
            finally
            {
                _lock.Release();
            }

            // Cross-volume move - lock released, now perform copy + delete
            DirectoryObject? copiedDirectory = null;
            try
            {
                copiedDirectory = await CopyAsync(source, destination, overwrite, progress, cancellationToken);
                await DeleteAsync(source, cancellationToken);

                // FINALIZATION
                return copiedDirectory;
            }
            catch
            {
                // Rollback: Attempt to delete the copied directory if it was created
                if (copiedDirectory != null)
                {
                    try
                    {
                        await DeleteAsync(copiedDirectory, cancellationToken);
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
        /// Asynchronously renames the specified directory to a new name and returns a new DirectoryObject representing
        /// the renamed directory.
        /// </summary>
        /// <remarks>The source DirectoryObject is disposed after the operation completes. The rename is
        /// performed atomically; if the operation fails, the original directory remains unchanged.</remarks>
        /// <param name="source">The DirectoryObject representing the directory to rename. Must not be null and must refer to an existing
        /// directory.</param>
        /// <param name="newName">The new name for the directory. Cannot be null, empty, or contain invalid path characters.</param>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the rename operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a DirectoryObject for the
        /// renamed directory.</returns>
        /// <exception cref="ArgumentException">Thrown if the new name is null, empty, or contains invalid characters.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be renamed due to an I/O error or insufficient permissions.</exception>
        public static async Task<DirectoryObject> RenameAsync(
            DirectoryObject source, 
            string newName,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ThrowIfInvalidDirectoryName(newName);
            var destination = source.ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);
            var _lock = source.Semaphore;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() => Directory.Move(source.FullPath, destination), cancellationToken);

                // FINALIZATION
                return new DirectoryObject(destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to rename directory from '{source.FullPath}' to '{destination}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static void ProcessDestinationPath(ref string destination, string dirname, bool overwrite = false)
        {
            try
            {
                // Verify the destination path. The destination provided should be a directory.
                destination = Path.GetFullPath(destination);
                if (File.Exists(destination))
                {
                    throw new InvalidOperationException($"The '{destination}' points to an existing file. Please provide a valid directory path.");
                }

                // Create the directory if it doesn't exist
                if (!Directory.Exists(destination))
                {
                    Directory.CreateDirectory(destination);
                }

                var fullPath = Path.Combine(destination, dirname);
                // Check if the directory already exists at the destination
                if (Directory.Exists(fullPath) && !overwrite)
                {
                    int count = 1;

                    // Generate a new directory name by appending a number
                    do
                    {
                        string tempDirName = $"{dirname} ({count++})";
                        fullPath = Path.Combine(destination, tempDirName);
                    } while (Directory.Exists(fullPath));
                }

                destination = fullPath;
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                // Re-throw business logic exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                // Wrap filesystem exceptions with context
                throw new InvalidOperationException($"Failed to process destination path '{destination}'.", ex);
            }
        }

        private static void ThrowIfInvalidDirectoryName(string dirname)
        {
            if (string.IsNullOrWhiteSpace(dirname))
            {
                throw new ArgumentException("Directory name cannot be null or empty.", nameof(dirname));
            }
            var invalidChars = Path.GetInvalidFileNameChars();
            if (dirname.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException($"Directory name '{dirname}' contains invalid characters.", nameof(dirname));
            }
        }
    }
}
