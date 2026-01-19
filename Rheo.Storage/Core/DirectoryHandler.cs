using Rheo.Storage.Contracts;
using Rheo.Storage.Information;
using System.Diagnostics;

namespace Rheo.Storage.Core
{
    /// <summary>
    /// Provides an abstract base class for managing and manipulating directories, including operations such as copying,
    /// moving, deleting, and renaming directories within a storage system.
    /// </summary>
    /// <remarks>DirectoryHandler encapsulates common directory management functionality and serves as a
    /// foundation for concrete directory handling implementations. It supports recursive operations, progress reporting
    /// for long-running tasks, and change notifications when directory state changes. Thread safety is provided for
    /// internal operations where necessary, but derived classes should ensure appropriate synchronization for extended
    /// scenarios.</remarks>
    public abstract partial class DirectoryHandler : StorageObject
    {
        private DirectoryInformation? _cachedInformation;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Initializes a new instance of the DirectoryHandler class for the specified directory path or name. Ensures
        /// that the directory exists, creating it if necessary.
        /// </summary>
        /// <param name="directoryNameOrPath">The name or full path of the directory to manage. If a relative path is provided, it is resolved based on
        /// the application's current working directory.</param>
        public DirectoryHandler(string directoryNameOrPath) : base(directoryNameOrPath, false)
        {
            // Ensure the Directory exists
            Directory.CreateDirectory(FullPath);
        }

        internal DirectoryHandler(DirectoryInformation info) : base(info.AbsolutePath, true)
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
                    _cachedInformation = new DirectoryInformation(FullPath);
                    _lastCacheTime = DateTime.UtcNow;
                }
                
                return _cachedInformation;
            }
            protected set
            {
                ThrowIfDisposed();
                _cachedInformation = (DirectoryInformation)value;
                _lastCacheTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Copies the contents of the current directory to the specified destination directory, including all
        /// subdirectories and files.
        /// </summary>
        /// <remarks>All files and subdirectories from the source directory are recursively copied to the
        /// destination. If overwrite is false and a file already exists at the destination, the operation may fail or
        /// skip the file, depending on the underlying file handling logic. Progress updates are reported cumulatively
        /// for the entire operation if a progress reporter is provided.</remarks>
        /// <param name="destination">The path to the destination directory where the contents will be copied. If the directory does not exist, it
        /// will be created.</param>
        /// <param name="overwrite">true to overwrite existing files in the destination directory; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation, including total bytes, bytes
        /// transferred, and transfer speed. May be null if progress reporting is not required.</param>
        /// <returns>A DirectoryInformation object representing the destination directory after the copy operation completes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails for any reason, such as an I/O error or invalid destination path.</exception>
        internal DirectoryInformation CopyInternal(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
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
                WaitForDirectoryAvailable(fullPath);    // Ensure source directory is accessible

                // Step 1: Create all directories first (including empty ones)
                // Ensure the destination root directory exists
                if (!Directory.Exists(destination))
                {
                    Directory.CreateDirectory(destination);
                }
                
                var directories = Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories)
                    .Select(dir => Path.Combine(destination, Path.GetRelativePath(fullPath, dir)));
                foreach (var targetDir in directories)
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }

                // Step 2: Copy all files using FileHandling infrastructure
                foreach (var filePath in files)
                {
                    var relativePath = Path.GetRelativePath(fullPath, filePath);
                    var targetFilePath = Path.Combine(destination, relativePath);
                    
                    using var fileObj = new FileObject(filePath);
                    
                    // Create a progress wrapper to aggregate progress
                    IProgress<StorageProgress>? fileProgress = null;
                    if (progress != null)
                    {
                        long previousBytes = bytesTransferred;
                        fileProgress = new Progress<StorageProgress>(sp =>
                        {
                            // Calculate delta since last update
                            var delta = sp.BytesTransferred - (bytesTransferred - previousBytes);
                            bytesTransferred += delta;
                            
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double bytesPerSecond = elapsedSeconds > 0 ? bytesTransferred / elapsedSeconds : 0;
                            
                            progress.Report(new StorageProgress
                            {
                                TotalBytes = totalBytes,
                                BytesTransferred = bytesTransferred,
                                BytesPerSecond = bytesPerSecond
                            });
                        });
                    }

                    // Use FileHandling.Copy for efficient file copying
                    var targetDir = Path.GetDirectoryName(targetFilePath)!;
                    fileObj.CopyInternal(targetDir, overwrite, fileProgress);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to copy directory from '{fullPath}' to '{destination}'.", ex);
            }

            // FINALIZATION - Return directory information
            return new DirectoryInformation(destination);
        }

        /// <summary>
        /// Deletes the directory represented by this instance and raises a change notification event.
        /// </summary>
        /// <remarks>If the directory does not exist, the deletion is treated as successful and a change
        /// notification is still raised. This method is intended for internal use and is not thread-safe beyond the
        /// internal locking provided.</remarks>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be deleted due to an I/O error or insufficient permissions.</exception>
        internal void DeleteInternal()
        {
            var path = FullPath; // Store path before raising event

            lock (StateLock)
            {
                try
                {
                    WaitForDirectoryAvailable(path);    // Ensure source directory is accessible

                    Directory.Delete(path, true);

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
            }
        }

        /// <summary>
        /// Moves the current directory to the specified destination path, optionally overwriting an existing directory.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. For cross-volume moves, the directory is copied to the destination and then deleted
        /// from the source. Progress reporting is only available for cross-volume moves; same-volume moves are
        /// typically instantaneous and do not provide detailed progress updates.</remarks>
        /// <param name="destination">The path to which the directory will be moved. This must be a valid directory path and cannot be null or
        /// empty.</param>
        /// <param name="overwrite">true to overwrite the destination directory if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. Progress is only reported for
        /// cross-volume moves.</param>
        /// <returns>A DirectoryInformation object representing the directory at its new location.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to an I/O error or insufficient permissions.</exception>
        internal DirectoryInformation MoveInternal(
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, Name, overwrite);

            // OPERATION
            lock (StateLock)
            {
                try
                {
                    WaitForDirectoryAvailable(FullPath);    // Ensure source directory is accessible

                    if (IsInTheSameRoot(destination))
                    {
                        // Handle overwrite for same-volume moves
                        if (overwrite && Directory.Exists(destination))
                        {
                            Directory.Delete(destination, true);
                        }

                        // Same volume move - fast operation (just directory entry update)
                        Directory.Move(FullPath, destination);

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
            }

            // Cross-volume move - release lock before calling nested operations
            DirectoryInformation? copiedInfo = null;
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
                // Rollback: Attempt to delete the copied directory if it was created
                if (copiedInfo != null)
                {
                    try 
                    { 
                        var tempObj = new DirectoryObject(copiedInfo.AbsolutePath);
                        tempObj.DeleteInternal();
                    }
                    catch { /* Suppress exceptions during rollback */ }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the current directory to the specified new name within the same parent directory.
        /// </summary>
        /// <param name="newName">The new name for the directory. Must be a valid directory name and cannot be null or empty.</param>
        /// <returns>A new DirectoryInformation instance representing the directory after it has been renamed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be renamed, such as when the destination already exists, the source or
        /// destination is in use, or access is denied.</exception>
        internal DirectoryInformation RenameInternal(string newName)
        {
            // INITIALIZATION
            ThrowIfInvalidDirectoryName(newName);
            var destination = ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);

            // OPERATION
            lock (StateLock)
            {
                try
                {
                    WaitForDirectoryAvailable(FullPath);    // Ensure source directory is accessible

                    Directory.Move(FullPath, destination);

                    // FINALIZATION - Create new information and raise event
                    var newInfo = new DirectoryInformation(destination);
                    RaiseChanged(StorageChangeType.Relocated, newInfo);
                    return newInfo;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to rename directory from '{FullPath}' to '{destination}'.", ex);
                }
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
