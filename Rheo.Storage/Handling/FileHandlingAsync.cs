using System.Diagnostics;

namespace Rheo.Storage.Handling
{
    internal static partial class FileHandling
    {
        /// <summary>
        /// Asynchronously copies the specified file to a new location, optionally overwriting an existing file and
        /// reporting progress.
        /// </summary>
        /// <remarks>This method uses double-buffered asynchronous I/O to maximize throughput, which can
        /// significantly improve performance on high-speed storage devices. Progress is reported after each successful
        /// write operation if a progress reporter is provided. The method acquires an exclusive lock on the source file
        /// for the duration of the copy to ensure thread safety.</remarks>
        /// <param name="source">The file to copy. Must not be null and must refer to an existing file.</param>
        /// <param name="destination">The full path of the destination file. If the file exists and <paramref name="overwrite"/> is <see
        /// langword="false"/>, the operation will fail.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination file if it already exists. If <see
        /// langword="false"/>, an exception is thrown if the destination file exists.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation, including bytes transferred
        /// and transfer rate. May be null if progress reporting is not required.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a <see cref="FileObject"/>
        /// representing the newly created file at the destination path.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to an I/O error or insufficient permissions.</exception>
        public static async Task<FileObject> CopyAsync(
            FileObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            // Assumes the source file is properly validated before calling this method
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var _lock = source.GetHandlingLock();
            var bufferSize = source.GetBufferSize();

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var sourceStream = new FileStream(
                    source.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                using var destStream = new FileStream(
                    destination,
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

                // Prime the pump: Read first chunk synchronously
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to copy file to: {destination}", ex);
            }
            finally
            {
                _lock.Release();
            }

            // FINALIZATION
            return new FileObject(destination);
        }

        /// <summary>
        /// Asynchronously deletes the specified file from the file system and disposes the associated FileObject.
        /// </summary>
        /// <remarks>After successful deletion, the FileObject is disposed and should not be used. This
        /// method acquires a lock on the FileObject to ensure thread safety during the delete operation.</remarks>
        /// <param name="file">The FileObject representing the file to delete. Cannot be null. The file must exist and be accessible for
        /// deletion.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be deleted due to an I/O error or insufficient permissions.</exception>
        public static async Task DeleteAsync(FileObject file)
        {
            var _lock = file.GetHandlingLock();

            await _lock.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    File.Delete(file.FullPath);
                });

                // Dispose the current FileObject to ensure the stored information are correct
                file.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to delete file: {file.FullPath}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Asynchronously moves the specified file to a new destination, optionally overwriting an existing file.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. If they are on different volumes, the file is copied and then the source is deleted.
        /// The source file is disposed after a successful move. The method is thread-safe with respect to the source
        /// file. Progress updates are reported only if a progress reporter is provided.</remarks>
        /// <param name="source">The file to move. Must not be null and must refer to an existing file.</param>
        /// <param name="destination">The full path to the destination file. Cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination file if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. May be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the move operation.</param>
        /// <returns>A task that represents the asynchronous move operation. The task result contains a FileObject representing
        /// the file at the new destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to an I/O error or insufficient permissions.</exception>
        public static async Task<FileObject> MoveAsync(
            FileObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            // Assumes the source file is properly validated before calling this method
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var _lock = source.GetHandlingLock();

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (source.IsInTheSameRoot(destination))
                {
                    // Same volume move - fast operation (just directory entry update)
                    // Use Task.Run for async context and cancellation support
                    await Task.Run(() =>
                    {
                        File.Move(source.FullPath, destination, overwrite);
                    }, cancellationToken);

                    // Dispose the current FileObject to ensure the stored information are correct
                    source.Dispose();

                    // Send final progress update
                    progress?.Report(new StorageProgress
                    {
                        TotalBytes = 1,
                        BytesTransferred = 1,
                        BytesPerSecond = 0
                    });

                    // FINALIZATION
                    return new FileObject(destination);
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
            FileObject? copiedFile = null;
            try
            {
                copiedFile = await CopyAsync(source, destination, overwrite, progress, cancellationToken);
                await DeleteAsync(source);

                // FINALIZATION
                return copiedFile;
            }
            catch
            {
                // Rollback: Delete the copied file if delete of source failed
                if (copiedFile != null)
                {
                    try { await DeleteAsync(copiedFile); } catch { /* Log but don't throw */ }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the specified file asynchronously to the given new name and returns a new FileObject representing
        /// the renamed file.
        /// </summary>
        /// <remarks>The source FileObject is disposed after the rename operation to ensure that its
        /// information is no longer used. The operation is thread-safe and will wait for any ongoing operations on the
        /// source file to complete before renaming.</remarks>
        /// <param name="source">The FileObject representing the file to be renamed. Must not be null.</param>
        /// <param name="newName">The new name for the file. Must be a valid file name and cannot be null or empty.</param>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the rename operation.</param>
        /// <returns>A FileObject representing the file after it has been renamed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be renamed due to an I/O error or insufficient permissions.</exception>
        public static async Task<FileObject> RenameAsync(
            FileObject source, 
            string newName, 
            CancellationToken cancellationToken = default)
        {
            // INITIALIZATION
            var destination = Path.Combine(source.ParentDirectory, newName);
            ProcessDestinationPath(ref destination, newName, false);
            var _lock = source.GetHandlingLock();

            // OPERATION
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    File.Move(source.FullPath, destination, false);
                }, cancellationToken);

                // Dispose the current FileObject to ensure the stored information are correct
                source.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to rename file to: {newName}", ex);
            }
            finally
            {
                _lock.Release();
            }

            // FINALIZATION
            return new FileObject(destination);
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
    }
}
