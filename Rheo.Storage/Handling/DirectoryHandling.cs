using System.Diagnostics;

namespace Rheo.Storage.Handling
{
    internal static partial class DirectoryHandling
    {
        /// <summary>
        /// Copies the entire directory and its contents to a new location, optionally overwriting existing files and
        /// reporting progress.
        /// </summary>
        /// <remarks>This method copies all files and subdirectories recursively. It leverages the existing
        /// file copy infrastructure for efficient I/O. Empty directories are preserved. Progress is aggregated across
        /// all file copy operations.</remarks>
        /// <param name="source">The source DirectoryObject representing the directory to copy. Must not be null.</param>
        /// <param name="destination">The destination path where the directory will be copied. If not absolute, it will be resolved.</param>
        /// <param name="overwrite">true to overwrite existing files; otherwise, false. If false and a file exists, an exception is thrown.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the overall copy operation, including total bytes
        /// and transfer rate. May be null.</param>
        /// <returns>A DirectoryObject representing the newly created directory at the destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the copy operation fails due to I/O errors or insufficient permissions.</exception>
        public static DirectoryObject Copy(
            DirectoryObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
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
                    FileHandling.Copy(fileObj, targetDir, overwrite, fileProgress);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to copy directory from '{fullPath}' to '{destination}'.", ex);
            }

            // FINALIZATION
            return new DirectoryObject(destination);
        }

        /// <summary>
        /// Deletes the specified directory and all its contents from the file system.
        /// </summary>
        /// <remarks>This method deletes the directory recursively, including all files and subdirectories.
        /// After successful deletion, the DirectoryObject is disposed and should not be used for further
        /// operations. The method acquires a lock on the DirectoryObject to ensure thread safety during the delete
        /// operation.</remarks>
        /// <param name="source">The DirectoryObject representing the directory to delete. Cannot be null.</param>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be deleted due to an I/O error or insufficient permissions.</exception>
        public static void Delete(DirectoryObject source)
        {
            lock (source.StateLock)
            {
                try
                {
                    var path = source.FullPath; // Store path before disposing

                    // Dispose the source object to release resources
                    source.Dispose();
                    Directory.Delete(path, true);
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory already deleted; treat as successful deletion
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to delete directory at '{source.FullPath}'.", ex);
                }
            }
        }

        /// <summary>
        /// Moves the specified directory to a new destination, optionally overwriting an existing directory.
        /// </summary>
        /// <remarks>If the source and destination are on the same volume, the move is performed as a fast
        /// directory entry update. If they are on different volumes, the directory is copied and then the source is
        /// deleted. The source directory is disposed after a successful move. Progress updates are reported during
        /// cross-volume moves.</remarks>
        /// <param name="source">The directory to move. Must not be null and must refer to an existing directory.</param>
        /// <param name="destination">The full path to the destination. Cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination directory if it exists; otherwise, false.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the move operation. May be null.</param>
        /// <returns>A DirectoryObject representing the directory at the new destination.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the move operation fails due to I/O errors or insufficient permissions.</exception>
        public static DirectoryObject Move(
            DirectoryObject source,
            string destination,
            bool overwrite,
            IProgress<StorageProgress>? progress = null)
        {
            // INITIALIZATION
            ProcessDestinationPath(ref destination, source.Name, overwrite);

            // OPERATION
            lock (source.StateLock)
            {
                try
                {
                    if (source.IsInTheSameRoot(destination))
                    {
                        // Handle overwrite for same-volume moves
                        if (overwrite && Directory.Exists(destination))
                        {
                            Directory.Delete(destination, true);
                        }

                        // Same volume move - fast operation (just directory entry update)
                        Directory.Move(source.FullPath, destination);

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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to move directory from '{source.FullPath}' to '{destination}'.", ex);
                }
            }

            // Cross-volume move - release lock before calling nested operations
            DirectoryObject? copiedDirectory = null;
            try
            {
                copiedDirectory = Copy(source, destination, overwrite, progress);
                Delete(source);

                // FINALIZATION
                return copiedDirectory;
            }
            catch
            {
                // Rollback: Attempt to delete the copied directory if it was created
                if (copiedDirectory != null)
                {
                    try { Delete(copiedDirectory); }
                    catch { /* Suppress exceptions during rollback */ }
                }
                throw;
            }
        }

        /// <summary>
        /// Renames the specified directory to a new name and returns a new DirectoryObject representing the renamed
        /// directory.
        /// </summary>
        /// <remarks>The source DirectoryObject is disposed after the operation completes. The rename is
        /// performed atomically; if the operation fails, the original directory remains unchanged.</remarks>
        /// <param name="source">The DirectoryObject representing the directory to rename. Must not be null and must refer to an existing
        /// directory.</param>
        /// <param name="newName">The new name for the directory. Cannot be null, empty, or contain invalid path characters.</param>
        /// <returns>A DirectoryObject representing the renamed directory.</returns>
        /// <exception cref="ArgumentException">Thrown if the new name is null, empty, or contains invalid characters.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the directory cannot be renamed due to an I/O error or insufficient permissions.</exception>
        public static DirectoryObject Rename(DirectoryObject source, string newName)
        {
            // INITIALIZATION
            ThrowIfInvalidDirectoryName(newName);
            var destination = source.ParentDirectory;
            ProcessDestinationPath(ref destination, newName, false);

            // OPERATION
            lock (source.StateLock)
            {
                try
                {
                    Directory.Move(source.FullPath, destination);

                    // FINALIZATION
                    return new DirectoryObject(destination);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"Failed to rename directory from '{source.FullPath}' to '{destination}'.", ex);
                }
            }
        }
    }
}
