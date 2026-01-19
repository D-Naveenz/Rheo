using System.Diagnostics;
using Rheo.Storage.Information;

namespace Rheo.Storage.Core
{
    public abstract partial class FileHandler
    {
        /// <summary>
        /// Copies the current file to the specified destination asynchronously, optionally overwriting an existing file
        /// and reporting progress.
        /// </summary>
        /// <remarks>The copy operation is performed asynchronously and is thread-safe. If the source file
        /// is locked by another process, the method waits until it becomes available. Progress updates are reported if
        /// a progress reporter is provided.</remarks>
        /// <param name="destination">The path to the destination file. Must be a valid file path. If the file already exists and <paramref
        /// name="overwrite"/> is <see langword="false"/>, the operation will fail.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination file if it already exists. Set to <see
        /// langword="true"/> to overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation. Can be <see langword="null"/>
        /// if progress reporting is not required.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A <see cref="FileInformation"/> object representing the copied file at the destination path.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to an I/O error or insufficient permissions.</exception>
        internal async Task<FileInformation> CopyInternalAsync(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);
            var _lock = Semaphore;
            var bufferSize = GetBufferSize();

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WaitForFileAvailableAsync(FullPath, cancellationToken: cancellationToken);    // Ensure no other operations are in progress

                using var sourceStream = new FileStream(
                    FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await CopyStreamToFileAsync(
                    sourceStream,
                    destination,
                    overwrite,
                    bufferSize,
                    progress,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to copy file to: {destination}", ex);
            }
            finally
            {
                _lock.Release();
            }

            // FINALIZATION - Return file information
            return new FileInformation(destination);
        }

        /// <summary>
        /// Deletes the underlying file asynchronously and raises a change event to notify listeners of the deletion.
        /// </summary>
        /// <remarks>If the file does not exist, the change event is still raised to indicate deletion.
        /// The operation is thread-safe and will wait for any ongoing file operations to complete before
        /// deleting.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be deleted due to an I/O error or insufficient permissions.</exception>
        internal async Task DeleteInternalAsync(CancellationToken cancellationToken = default)
        {
            var _lock = Semaphore;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var path = FullPath; // Store path before raising event
                await WaitForFileAvailableAsync(path, cancellationToken: cancellationToken);    // Ensure no other operations are in progress

                await Task.Run(() =>
                {
                    File.Delete(path);
                }, cancellationToken);

                // Raise the Changed event to notify deletion
                RaiseChanged(StorageChangeType.Deleted, null);
            }
            catch (FileNotFoundException)
            {
                // File already deleted - still raise the event
                RaiseChanged(StorageChangeType.Deleted, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to delete file: {FullPath}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Moves the current file to the specified destination path asynchronously, optionally overwriting an existing
        /// file.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. For cross-volume moves, the file is copied and then deleted from the source
        /// location. Progress updates are reported if a progress reporter is provided. The operation is thread-safe and
        /// will wait for any ongoing file operations to complete before proceeding.</remarks>
        /// <param name="destination">The full path to the destination file. Must be a valid file path and cannot be null or empty.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination file if it already exists. If <see
        /// langword="true"/>, the destination file will be replaced.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation, including bytes transferred
        /// and total bytes.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the move operation.</param>
        /// <returns>A <see cref="FileInformation"/> object representing the file at its new location after the move completes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be moved to the specified destination due to I/O errors or insufficient
        /// permissions.</exception>
        internal async Task<FileInformation> MoveInternalAsync(
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
                await WaitForFileAvailableAsync(FullPath, cancellationToken: cancellationToken);    // Ensure no other operations are in progress

                if (IsInTheSameRoot(destination))
                {
                    // Same volume move - fast operation (just directory entry update)
                    await Task.Run(() =>
                    {
                        File.Move(FullPath, destination, overwrite);
                    }, cancellationToken);

                    // Send final progress update
                    progress?.Report(new StorageProgress
                    {
                        TotalBytes = 1,
                        BytesTransferred = 1,
                        BytesPerSecond = 0
                    });

                    // FINALIZATION - Create and return new information
                    var newInfo = new FileInformation(destination);
                    RaiseChanged(StorageChangeType.Relocated, newInfo);
                    return newInfo;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to move file to: {destination}", ex);
            }
            finally
            {
                _lock.Release();
            }

            // Cross-volume move - release lock before calling nested operations
            // This prevents deadlock since CopyAsync and DeleteAsync acquire the same lock
            FileInformation? copiedInfo = null;
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
                // Rollback: Delete the copied file if delete of source failed
                if (copiedInfo != null)
                {
                    try 
                    { 
                        var tempObj = new FileObject(copiedInfo.AbsolutePath);
                        await tempObj.DeleteInternalAsync(cancellationToken); 
                    } 
                    catch { /* Log but don't throw */ }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the current file to the specified new name asynchronously within its parent directory.
        /// </summary>
        /// <remarks>This method acquires an exclusive lock on the file during the rename operation to
        /// prevent concurrent access. The file is moved within its current parent directory. If the operation is
        /// canceled via the provided token, the file remains unchanged.</remarks>
        /// <param name="newName">The new name for the file. Must be a valid file name and cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the rename operation.</param>
        /// <returns>A <see cref="FileInformation"/> instance representing the file after it has been renamed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file cannot be renamed due to I/O errors or insufficient permissions.</exception>
        internal async Task<FileInformation> RenameInternalAsync(
            string newName,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            ThrowIfInvalidFileName(newName);
            var destination = ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);
            var _lock = Semaphore;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WaitForFileAvailableAsync(FullPath, cancellationToken: cancellationToken);    // Ensure no other operations are in progress

                await Task.Run(() =>
                {
                    File.Move(FullPath, destination, false);
                }, cancellationToken);

                // FINALIZATION - Create new information and raise event
                var newInfo = new FileInformation(destination);
                RaiseChanged(StorageChangeType.Relocated, newInfo);
                return newInfo;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to rename file to: {newName}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Asynchronously writes the contents of the specified stream to the target file, replacing any existing data.
        /// </summary>
        /// <remarks>This method acquires an internal lock to ensure thread safety during the write
        /// operation. The file is overwritten with the contents of the provided stream. Progress updates are reported
        /// if a progress reporter is supplied.</remarks>
        /// <param name="sourceStream">The stream containing the data to be written to the file. The stream must be readable and positioned at the
        /// beginning.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the number of bytes written during the operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the write operation.</param>
        /// <returns>A FileInformation object representing the file after the write operation has completed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the write operation fails due to an I/O error or insufficient permissions.</exception>
        internal async Task<FileInformation> WriteInternalAsync(
            Stream sourceStream,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            var _lock = Semaphore;
            var bufferSize = GetBufferSize();
            var destination = FullPath;

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                // Ensure we start reading from the beginning
                sourceStream.Seek(0, SeekOrigin.Begin);

                await CopyStreamToFileAsync(
                    sourceStream,
                    destination,
                    true,
                    bufferSize,
                    progress,
                    cancellationToken);

                // FINALIZATION - Create new information and raise event
                var newInfo = new FileInformation(destination);
                RaiseChanged(StorageChangeType.Modified, newInfo);
                return newInfo;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to write to file: {destination}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Core implementation for copying data from a stream to a file using double-buffered asynchronous I/O.
        /// </summary>
        /// <remarks>This internal method implements the double-buffering pattern to achieve overlapped I/O,
        /// which significantly improves performance on high-speed storage. The method does not acquire locks or manage
        /// FileObject lifecycle - callers are responsible for synchronization and resource management.</remarks>
        /// <param name="sourceStream">The source stream to read from. Must support async reads and have a known length.</param>
        /// <param name="destinationPath">The full path to the destination file.</param>
        /// <param name="overwrite">Whether to overwrite the destination file if it exists.</param>
        /// <param name="bufferSize">The buffer size to use for reading and writing.</param>
        /// <param name="progress">Optional progress reporter for tracking the operation.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        private static async Task CopyStreamToFileAsync(
            Stream sourceStream,
            string destinationPath,
            bool overwrite,
            int bufferSize,
            IProgress<StorageProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (overwrite && File.Exists(destinationPath))
            {
                // Ensure no other operations are in progress
                await WaitForFileAvailableAsync(destinationPath, cancellationToken: cancellationToken);
            }

            using var destStream = new FileStream(
                destinationPath,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var totalBytes = sourceStream.Length;
            var totalRead = 0L;
            var stopwatch = Stopwatch.StartNew();

            // Double buffering: Allows overlapped I/O (read next chunk while writing current chunk)
            // This achieves 2-3x performance on high-speed storage by keeping both disks busy
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];
            var currentBuffer = buffer1;  // Buffer holding data to write
            var nextBuffer = buffer2;     // Buffer to read next chunk into

            // Prime the pump: Read first chunk
            int bytesRead = await sourceStream.ReadAsync(currentBuffer, cancellationToken);

            while (bytesRead > 0)
            {
                // OVERLAPPED I/O SECTION:
                // 1. Start reading next chunk (non-blocking - returns Task immediately)
                var readTask = sourceStream.ReadAsync(nextBuffer, cancellationToken);

                // 2. Write current chunk (runs concurrently with the read above)
                //    While this writes to disk, the read operation is filling nextBuffer
                await destStream.WriteAsync(currentBuffer.AsMemory(0, bytesRead), cancellationToken);

                totalRead += bytesRead;

                if (progress != null)
                {
                    // Report progress after each successful write
                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    double bytesPerSecond = elapsedSeconds > 0 ? totalRead / elapsedSeconds : 0;
                    progress.Report(new StorageProgress
                    {
                        TotalBytes = totalBytes,
                        BytesTransferred = totalRead,
                        BytesPerSecond = bytesPerSecond
                    });
                }

                // Swap buffers: What was "next" becomes "current" for next iteration
                // This is thread-safe because we await readTask below, ensuring no concurrent access
                (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);

                // 3. Ensure the read operation completed before continuing
                //    This guarantees nextBuffer is fully populated before we write it
                bytesRead = await readTask;
            }
        }

        private static void ProcessDestinationPath(ref string destination, string filename, bool overwrite = false)
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

                var fullPath = Path.Combine(destination, filename);
                // Check if the file already exists at the destination
                if (File.Exists(fullPath) && !overwrite)
                {
                    int count = 1;
                    string fileNameOnly = Path.GetFileNameWithoutExtension(filename);
                    string extension = Path.GetExtension(filename);

                    // Generate a new filename by appending a number
                    do
                    {
                        string tempFileName = $"{fileNameOnly} ({count++}){extension}";
                        fullPath = Path.Combine(destination, tempFileName);
                    } while (File.Exists(fullPath));
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

        private static void ThrowIfInvalidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"The file name '{fileName}' is invalid.", nameof(fileName));
            }
        }
    }
}
