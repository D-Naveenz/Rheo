using Rheo.Storage.Contracts;
using Rheo.Storage.Information;
using System.Diagnostics;

namespace Rheo.Storage.Core
{
    /// <summary>
    /// Provides an abstract base class for handling file operations, including copying, moving, renaming, deleting, and
    /// writing file data, with support for progress reporting and change notifications.
    /// </summary>
    /// <remarks>FileHandler encapsulates core file management functionality and exposes methods for
    /// manipulating files in a thread-safe manner. It maintains file metadata and raises events to notify changes such
    /// as deletion, relocation, or modification. This class is intended to be subclassed for specialized file handling
    /// scenarios and is not intended for direct instantiation outside of derived types.</remarks>
    public abstract partial class FileHandler : StorageObject
    {
        private FileInformation? _cachedInformation;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Initializes a new instance of the FileHandler class for the specified file path or name. Creates the file if
        /// it does not already exist.
        /// </summary>
        /// <remarks>The file is opened and immediately closed to ensure its existence without locking it
        /// for further operations. The file is shared for both reading and writing, allowing other processes to access
        /// it concurrently.</remarks>
        /// <param name="fileNameOrPath">The full path or name of the file to be handled. If the file does not exist, it will be created.</param>
        public FileHandler(string fileNameOrPath) : base(fileNameOrPath, false)
        {
            // Ensure the file exists without holding a stream open
            // Use FileShare.ReadWrite to avoid blocking other operations
            File.Open(FullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite).Dispose();
        }

        internal FileHandler(FileInformation info) : base(info.AbsolutePath, true)
        {
            _cachedInformation = info;
            _lastCacheTime = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public override IStorageInformation Information
        {
            get
            {
                ThrowIfDisposed();
                
                // Use time-based cache: refresh if cache is older than validity period
                if (_cachedInformation == null || DateTime.UtcNow - _lastCacheTime > CacheValidityPeriod)
                {
                    _cachedInformation = new FileInformation(FullPath);
                    _lastCacheTime = DateTime.UtcNow;
                }
                
                return _cachedInformation;
            }
            protected set
            {
                ThrowIfDisposed();
                lock (StateLock)
                {
                    _cachedInformation = (FileInformation)value;
                    _lastCacheTime = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Copies the current file to the specified destination path, optionally overwriting an existing file and
        /// reporting progress.
        /// </summary>
        /// <param name="destination">The full path to the destination file. Must not be null or empty.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination file if it already exists. If <see
        /// langword="false"/>, the operation will fail if the file exists.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation. If null, no progress is
        /// reported.</param>
        /// <returns>A <see cref="FileInformation"/> instance representing the copied file at the destination path.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to I/O errors or insufficient permissions.</exception>
        internal FileInformation CopyInternal(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);
            var bufferSize = GetBufferSize();

            // OPERATION
            lock(StateLock)
            {
                WaitForFileAvailable(FullPath);    // Ensure no other operations are in progress

                try
                {
                    using var sourceStream = new FileStream(
                        FullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize,
                        FileOptions.SequentialScan);

                    CopyStreamToFile(
                        sourceStream,
                        destination,
                        overwrite,
                        bufferSize,
                        progress);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to copy file to: {destination}", ex);
                }
            }

            // FINALIZATION - Return file information
            return new FileInformation(destination);
        }

        /// <summary>
        /// Deletes the underlying file and raises a change notification to indicate that the file has been deleted.
        /// </summary>
        /// <remarks>This method waits for any ongoing operations on the file to complete before
        /// attempting deletion. If the file does not exist, a deletion notification is still raised. This method is
        /// intended for internal use and is not thread-safe beyond its own locking mechanism.</remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be deleted due to an I/O error or insufficient permissions.</exception>
        internal void DeleteInternal()
        {
            lock(StateLock)
            {
                WaitForFileAvailable(FullPath);    // Ensure no other operations are in progress

                try
                {
                    var path = FullPath; // Store path before raising event

                    File.Delete(path);
                    
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
            }
        }

        /// <summary>
        /// Moves the current file to the specified destination path, optionally overwriting an existing file and
        /// reporting progress.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move operation is performed
        /// as a fast directory entry update. For cross-volume moves, the file is copied to the destination and the
        /// source is deleted. The method raises a relocation event upon successful completion.</remarks>
        /// <param name="destination">The full path to the destination file. Must be a valid file path and cannot be null or empty.</param>
        /// <param name="overwrite">Specifies whether to overwrite the destination file if it already exists. Set to <see langword="true"/> to
        /// overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. If not specified, progress
        /// will not be reported.</param>
        /// <returns>A <see cref="FileInformation"/> object representing the file at its new location after the move operation
        /// completes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be moved to the specified destination due to I/O errors or insufficient
        /// permissions.</exception>
        internal FileInformation MoveInternal(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);

            // OPERATION
            lock(StateLock)
            {
                WaitForFileAvailable(FullPath);    // Ensure no other operations are in progress

                try
                {
                    if (IsInTheSameRoot(destination))
                    {
                        // Same volume move - fast operation (just directory entry update)
                        File.Move(FullPath, destination, overwrite);

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
            }

            // Cross-volume move - release lock before calling nested operations
            FileInformation? copiedInfo = null;
            try
            {
                copiedInfo = CopyInternal(destination, overwrite, progress);
                DeleteInternal();

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
                        tempObj.DeleteInternal();
                } 
                catch { /* Log but don't throw */ }
            }
            throw;
        }
    }

        /// <summary>
        /// Renames the current file to the specified new name within the same directory and returns updated file
        /// information.
        /// </summary>
        /// <remarks>This method does not move the file to a different directory; it only changes the
        /// file's name within its existing parent directory. The operation is performed atomically and raises a change
        /// event upon successful completion.</remarks>
        /// <param name="newName">The new name for the file. Must be a valid file name and cannot be null or empty.</param>
        /// <returns>A <see cref="FileInformation"/> instance representing the file after it has been renamed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the file cannot be renamed due to I/O errors or insufficient permissions.</exception>
        internal FileInformation RenameInternal(string newName)
        {
            // INITIALIZATION
            ThrowIfInvalidFileName(newName);
            var destination = ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);

            // OPERATION
            lock(StateLock)
            {
                WaitForFileAvailable(FullPath);    // Ensure no other operations are in progress

                try
                {
                    File.Move(FullPath, destination, false);
                    
                    // FINALIZATION - Create new information and raise event
                    var newInfo = new FileInformation(destination);
                    RaiseChanged(StorageChangeType.Relocated, newInfo);
                    return newInfo;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to rename file to: {newName}", ex);
                }
            }
        }

        /// <summary>
        /// Writes the contents of the specified stream to the file represented by this instance, replacing any existing
        /// data.
        /// </summary>
        /// <remarks>The method overwrites the file with the contents of the provided stream. The
        /// operation is performed atomically and is thread-safe. Progress updates are reported if a progress reporter
        /// is supplied.</remarks>
        /// <param name="sourceStream">The stream containing the data to write to the file. The stream must be readable and positioned at the
        /// beginning.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the number of bytes written. If null, progress is
        /// not reported.</param>
        /// <returns>A FileInformation object representing the updated state of the file after the write operation completes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the write operation fails due to an I/O error or insufficient permissions.</exception>
        internal FileInformation WriteInternal(
            Stream sourceStream,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            var bufferSize = GetBufferSize();
            var destination = FullPath;

            // OPERATION
            lock(StateLock)
            {
                try
                {
                    // Ensure we start reading from the beginning
                    sourceStream.Seek(0, SeekOrigin.Begin);

                    CopyStreamToFile(
                        sourceStream,
                        destination,
                        true,
                        bufferSize,
                        progress);
                    
                    // FINALIZATION - Create new information and raise event
                    var newInfo = new FileInformation(destination);
                    RaiseChanged(StorageChangeType.Modified, newInfo);
                    return newInfo;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to write to file: {destination}", ex);
                }
            }
        }

        /// <summary>
        /// Core implementation for copying data from a stream to a file using synchronous I/O.
        /// </summary>
        /// <remarks>This internal method implements buffered stream copying for reliable file transfer. The
        /// method does not acquire locks or manage FileObject lifecycle - callers are responsible for synchronization
        /// and resource management.</remarks>
        /// <param name="sourceStream">The source stream to read from. Must support reads and have a known length.</param>
        /// <param name="destinationPath">The full path to the destination file.</param>
        /// <param name="overwrite">Whether to overwrite the destination file if it exists.</param>
        /// <param name="bufferSize">The buffer size to use for reading and writing.</param>
        /// <param name="progress">Optional progress reporter for tracking the operation.</param>
        private static void CopyStreamToFile(
            Stream sourceStream,
            string destinationPath,
            bool overwrite,
            int bufferSize,
            IProgress<StorageProgress>? progress)
        {
            if (overwrite && File.Exists(destinationPath))
            {
                // Ensure no other operations are in progress
                WaitForFileAvailable(destinationPath);
            }

            using var destStream = new FileStream(
                destinationPath,
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
    }
}
