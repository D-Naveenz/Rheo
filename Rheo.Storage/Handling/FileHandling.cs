using System.Diagnostics;

namespace Rheo.Storage.Handling
{
    internal static partial class FileHandling
    {
        /// <summary>
        /// Copies the specified file to a new location, optionally overwriting an existing file and reporting progress.
        /// </summary>
        /// <remarks>This method uses synchronous I/O operations. Progress is reported after each successful
        /// write operation if a progress reporter is provided. The method acquires an exclusive lock on the source file
        /// for the duration of the copy to ensure thread safety. For better performance on high-speed storage, consider
        /// using the asynchronous version.</remarks>
        /// <param name="source">The file to copy. Must not be null and must refer to an existing file.</param>
        /// <param name="destination">The full path of the destination file. If the file exists and <paramref name="overwrite"/> is <see
        /// langword="false"/>, the operation will fail.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination file if it already exists. If <see
        /// langword="false"/>, an exception is thrown if the destination file exists.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation, including bytes transferred
        /// and transfer rate. May be null if progress reporting is not required.</param>
        /// <returns>A <see cref="FileObject"/> representing the newly created file at the destination path.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to an I/O error or insufficient permissions.</exception>
        public static FileObject Copy(
            FileObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var _lock = source.GetHandlingLock();
            var bufferSize = source.GetBufferSize();

            // OPERATION
            _lock.Wait();
            try
            {
                using var sourceStream = new FileStream(
                    source.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    FileOptions.SequentialScan);

                using var destStream = new FileStream(
                    destination,
                    overwrite ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize,
                    FileOptions.SequentialScan);

                var totalBytes = sourceStream.Length;
                var totalRead = 0L;
                var stopwatch = Stopwatch.StartNew();
                var buffer = new byte[bufferSize];
                int bytesRead;

                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destStream.Write(buffer, 0, bytesRead);
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
        /// Deletes the specified file from the file system and disposes the associated FileObject.
        /// </summary>
        /// <remarks>After successful deletion, the FileObject is disposed and should not be used. This
        /// method acquires a lock on the FileObject to ensure thread safety during the delete operation.</remarks>
        /// <param name="file">The FileObject representing the file to delete. Cannot be null. The file must exist and be accessible for
        /// deletion.</param>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be deleted due to an I/O error or insufficient permissions.</exception>
        public static void Delete(FileObject file)
        {
            var _lock = file.GetHandlingLock();

            _lock.Wait();
            try
            {
                File.Delete(file.FullPath);

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
        /// Moves the specified file to a new destination, optionally overwriting an existing file.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. If they are on different volumes, the file is copied and then the source is deleted.
        /// The source file is disposed after a successful move. The method is thread-safe with respect to the source
        /// file. Progress updates are reported only if a progress reporter is provided.</remarks>
        /// <param name="source">The file to move. Must not be null and must refer to an existing file.</param>
        /// <param name="destination">The full path to the destination file. Cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination file if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. May be null.</param>
        /// <returns>A <see cref="FileObject"/> representing the file at the new destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to an I/O error or insufficient permissions.</exception>
        public static FileObject Move(
            FileObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, source.Name, overwrite);
            var _lock = source.GetHandlingLock();

            // OPERATION
            _lock.Wait();
            try
            {
                if (source.IsInTheSameRoot(destination))
                {
                    // Same volume move - fast operation (just directory entry update)
                    File.Move(source.FullPath, destination, overwrite);

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
            // This prevents deadlock since Copy and Delete acquire the same lock
            FileObject? copiedFile = null;
            try
            {
                copiedFile = Copy(source, destination, overwrite, progress);
                Delete(source);

                // FINALIZATION
                return copiedFile;
            }
            catch
            {
                // Rollback: Delete the copied file if delete of source failed
                if (copiedFile != null)
                {
                    try { Delete(copiedFile); } catch { /* Log but don't throw */ }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the specified file to the given new name and returns a new FileObject representing the renamed file.
        /// </summary>
        /// <remarks>The source FileObject is disposed after the rename operation to ensure that its
        /// information is no longer used. The operation is thread-safe and will wait for any ongoing operations on the
        /// source file to complete before renaming.</remarks>
        /// <param name="source">The FileObject representing the file to be renamed. Must not be null.</param>
        /// <param name="newName">The new name for the file. Must be a valid file name and cannot be null or empty.</param>
        /// <returns>A <see cref="FileObject"/> representing the file after it has been renamed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be renamed due to an I/O error or insufficient permissions.</exception>
        public static FileObject Rename(FileObject source, string newName)
        {
            // INITIALIZATION
            var destination = Path.Combine(source.ParentDirectory, newName);
            ProcessDestinationPath(ref destination, newName, false);
            var _lock = source.GetHandlingLock();

            // OPERATION
            _lock.Wait();
            try
            {
                File.Move(source.FullPath, destination, false);

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
    }
}
