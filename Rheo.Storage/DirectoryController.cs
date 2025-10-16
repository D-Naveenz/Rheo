using Rheo.Storage.Info;
using System.Diagnostics;

namespace Rheo.Storage
{
    /// <summary>
    /// Provides functionality for managing and interacting with directories in the file system.
    /// </summary>
    /// <remarks>The <see cref="DirectoryController"/> class extends <see cref="StorageController"/> to
    /// provide directory-specific operations, such as retrieving files, subdirectories, and metadata about the
    /// directory. It also implements <see cref="IStorageInfoContainer{T}"/> to expose detailed information about the
    /// directory through the <see cref="Information"/> property. <para> This class supports operations such as copying,
    /// moving, renaming, and deleting directories, as well as retrieving directory contents and metadata. It ensures
    /// proper handling of exceptions and provides additional context for error scenarios. </para> <para> Use this class
    /// when working with directories in a structured and programmatic way, especially when additional metadata or
    /// advanced operations are required. </para></remarks>
    public class DirectoryController : StorageController, IStorageInfoContainer<DirectoryInfomation>
    {
        private readonly DirectoryInfomation? _storageInfo;

        public DirectoryController(string fileNameOrPath, bool isInfoRequired = true) : base(fileNameOrPath, AssertAs.Directory)
        {
            // Initialize storage information if required
            try
            {
                if (isInfoRequired)
                {
                    _storageInfo = Activator.CreateInstance(typeof(DirectoryInfomation), FullPath) as DirectoryInfomation;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not create an instance of type {typeof(DirectoryInfomation).FullName}.", ex);
            }
        }

        public override DateTime CreatedAt => Directory.GetCreationTime(FullPath);

        public override bool IsAvailable => Directory.Exists(FullPath);

        /// <inheritdoc cref="DirectoryInfomation.NoOfFiles"/>/>
        public int NoOfFiles => Information.NoOfFiles;

        /// <inheritdoc cref="DirectoryInfomation.NoOfDirectories"/>/>
        public int NoOfDirectories => Information.NoOfDirectories;

        public DirectoryInfomation Information => _storageInfo ?? throw new InvalidOperationException("Storage information is not available.");

        public string ContentType => Information.MimeType;

        public string? DisplayName => Information.DisplayName;

        public string? DisplayType => Information.TypeName;

        public bool IsReadOnly => Information.AttributeFlags.HasFlag(FileAttributes.ReadOnly);

        public bool IsHidden => Information.AttributeFlags.HasFlag(FileAttributes.Hidden);

        public bool IsSystem => Information.AttributeFlags.HasFlag(FileAttributes.System);

        public bool IsTemporary => Information.AttributeFlags.HasFlag(FileAttributes.Temporary);

        #region Methods
        /// <summary>
        /// Retrieves the file names from the directory represented by this instance, based on the specified search
        /// pattern and search option.
        /// </summary>
        /// <remarks>This method uses the <see cref="Directory.GetFiles(string, string, SearchOption)"/>
        /// method internally to retrieve the file names.</remarks>
        /// <param name="searchPattern">The search string to match against the names of files in the directory. The default value is "*", which
        /// matches all files.</param>
        /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories. The default value is <see
        /// cref="SearchOption.TopDirectoryOnly"/>.</param>
        /// <returns>An array of strings containing the full paths of the files that match the specified
        /// search pattern and search option.</returns>
        /// <exception cref="IOException">Thrown if an I/O error occurs while accessing the directory or its contents.</exception>
        public string[] GetFiles(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            try
            {
                return Directory.GetFiles(FullPath, searchPattern, searchOption);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Rethrow the exception with additional context
                throw new IOException($"Error accessing files in directory '{FullPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves a file from the specified relative path within the current directory.
        /// </summary>
        /// <remarks>The method combines the provided relative path with the current directory's full path
        /// to locate the file. Ensure that the relative path is valid and points to an existing file within the
        /// directory.</remarks>
        /// <param name="relativePath">The relative path to the file, starting from the current directory. The path must not be rooted.</param>
        /// <returns>A <see cref="FileController"/> instance representing the file at the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is an absolute path.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist at the specified relative path.</exception>
        public FileController GetFile(string relativePath)
        {
            // Verify that the relativePath is indeed relative
            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("The provided path must be relative.", nameof(relativePath));
            }
            var fullPath = Path.Combine(FullPath, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"The file '{relativePath}' does not exist in the directory '{FullPath}'.", fullPath);
            }

            return new FileController(fullPath);
        }

        /// <summary>
        /// Retrieves the names of subdirectories in the current directory that match the specified search pattern and
        /// search option.
        /// </summary>
        /// <remarks>This method wraps <see cref="Directory.GetDirectories(string, string,
        /// SearchOption)"/> and provides additional context in the exception message if an error occurs.</remarks>
        /// <param name="searchPattern">The search string to match against the names of subdirectories. The default value is "*", which matches all
        /// subdirectories.</param>
        /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories. The default value is <see
        /// cref="SearchOption.TopDirectoryOnly"/>.</param>
        /// <returns>An array of the full paths of subdirectories that match the specified search pattern and
        /// search option.</returns>
        /// <exception cref="IOException">Thrown if an I/O error occurs while accessing the file system, or if an error occurs while retrieving
        /// directories.</exception>
        public string[] GetDirectories(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            try
            {
                return Directory.GetDirectories(FullPath, searchPattern, searchOption);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Rethrow the exception with additional context
                throw new IOException($"Error accessing directories in '{FullPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves a <see cref="DirectoryController"/> representing the directory at the specified relative path.
        /// </summary>
        /// <remarks>This method combines the specified <paramref name="relativePath"/> with the base
        /// directory's full path to locate the target directory. Ensure that the relative path is valid and points to
        /// an existing directory.</remarks>
        /// <param name="relativePath">The relative path to the directory, relative to the base directory represented by this instance. The path
        /// must not be rooted.</param>
        /// <returns>A <see cref="DirectoryController"/> representing the directory at the specified relative path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is a rooted path.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by <paramref name="relativePath"/> does not exist.</exception>
        public DirectoryController GetDirectory(string relativePath)
        {
            // Verify that the relativePath is indeed relative
            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("The provided path must be relative.", nameof(relativePath));
            }
            var fullPath = Path.Combine(FullPath, relativePath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The directory '{relativePath}' does not exist in the directory '{FullPath}'.");
            }
            return new DirectoryController(fullPath);
        }

        public override long GetSize(UOM uom = UOM.KB)
        {
            long sizeInBytes = 0;
            try
            {
                // Sum the sizes of all files in the directory and its subdirectories
                var allFiles = Directory.GetFiles(FullPath, "*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    var fileInfo = new FileInfo(file);
                    sizeInBytes += fileInfo.Length;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Handle the case where access is denied
                return -1; // or any other value indicating access is denied
            }
            return uom switch
            {
                UOM.Bytes => sizeInBytes,
                UOM.KB => sizeInBytes / 1024,
                UOM.MB => sizeInBytes / (1024 * 1024),
                UOM.GB => sizeInBytes / (1024 * 1024 * 1024),
                UOM.TB => sizeInBytes / (1024L * 1024 * 1024 * 1024),
                _ => sizeInBytes / 1024,
            };
        }

        public override async Task CopyAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ProcessDestinationPath(ref destination, overwrite);

            var files = Directory.GetFiles(FullPath, "*", SearchOption.AllDirectories);
            var totalBytes = files.Sum(file => new FileInfo(file).Length);
            long bytesTransferred = 0;

            var bufferSize = BufferSize;
            var stopwatch = Stopwatch.StartNew();

            // Create all directories first (including empty ones)
            var directories = Directory.GetDirectories(FullPath, "*", SearchOption.AllDirectories);
            foreach (var dir in directories)
            {
                var relativeDir = Path.GetRelativePath(FullPath, dir);
                var targetDir = Path.Combine(destination, relativeDir);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
            }

            var semaphore = new SemaphoreSlim(maxConcurrent > 0 ? maxConcurrent : 1);
            var exceptions = new List<Exception>();

            var copyTasks = files.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(FullPath, file);
                    var targetFilePath = Path.Combine(destination, relativePath);

                    var targetDirectory = Path.GetDirectoryName(targetFilePath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory!);
                    }

                    using var sourceStream = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize,
                        true);

                    using var destStream = new FileStream(
                        targetFilePath,
                        overwrite ? FileMode.Create : FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize,
                        true);

                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        Interlocked.Add(ref bytesTransferred, bytesRead);

                        if (progress != null)
                        {
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double bytesPerSecond = elapsedSeconds > 0 ? bytesTransferred / elapsedSeconds : 0;
                            progress.Report(new StorageProgress
                            {
                                TotalBytes = totalBytes,
                                BytesTransferred = bytesTransferred,
                                BytesPerSecond = bytesPerSecond
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(copyTasks);

            if (exceptions.Count > 0)
            {
                throw new AggregateException("One or more files failed to copy.", exceptions);
            }
        }

        public override Task DeleteAsync()
        {
            return Task.Run(() => Directory.Delete(FullPath, true));
        }

        public override async Task MoveAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ProcessDestinationPath(ref destination, overwrite);

            // If source and destination are on the same volume, use Directory.Move for atomic move
            if (AreOnSameVolume(FullPath, destination))
            {
                // If overwrite is true and destination exists, delete it first
                if (overwrite && Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }
                await Task.Run(() => Directory.Move(FullPath, destination), cancellationToken);
                Name = Path.GetFileName(destination);

                // Report progress as complete
                progress?.Report(new StorageProgress
                {
                    TotalBytes = 1,
                    BytesTransferred = 1,
                    BytesPerSecond = 0
                });
            }
            else
            {
                // Otherwise, perform copy + delete (cross-volume)
                await CopyAsync(destination, overwrite, maxConcurrent, progress, cancellationToken);
                await DeleteAsync();
            }

            Name = Path.GetFileName(destination);
        }

        public override async Task RenameAsync(string newName)
        {
            // Validate newName
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name must not be null or whitespace.", nameof(newName));

            // Verify the name does not contain invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (newName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException("The name contains invalid characters.", nameof(newName));
            }

            // Get parent directory and new full path
            var parentDir = Path.GetDirectoryName(FullPath) ?? throw new InvalidOperationException("Cannot determine parent directory.");
            var newFullPath = Path.Combine(parentDir, newName);

            // Ensure the new path does not already exist
            if (Directory.Exists(newFullPath))
                throw new IOException($"A directory with the name '{newName}' already exists in '{parentDir}'.");

            // Perform the rename (move)
            await Task.Run(() => Directory.Move(FullPath, newFullPath));

            // Update internal state
            Name = newName;
        }

        public override string ToString()
        {
            return Stringify(AssertAs.Directory, DisplayName, DisplayType);
        }
        #endregion
    }
}
