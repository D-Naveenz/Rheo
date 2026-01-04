using Rheo.Storage.Contracts;

namespace Rheo.Storage
{
    /// <summary>
    /// Represents an abstract base class for managing storage resources, such as files or directories.
    /// </summary>
    /// <remarks>The <see cref="StorageObject"/> class provides a foundation for working with storage
    /// resources,  including properties for accessing metadata (e.g., name, size, creation date) and methods for
    /// performing  common operations such as copying, moving, renaming, and deleting resources.  Derived classes must
    /// implement abstract members to provide specific behavior for the storage type.</remarks>
    public abstract class StorageObject : IDisposable
    {
        private const uint MIN_BUFFER_SIZE = 1024; // 1KB
        private const uint MAX_BUFFER_SIZE = 16 * 1024 * 1024; // 16MB

        protected IStorageInformation _informationInternal;

        private readonly Lock _infoLock = new();

        /// <summary>
        /// Initializes a new instance of the StorageObject class for the specified file or path.
        /// </summary>
        /// <remarks>If the specified path does not exist, the constructor creates the necessary parent
        /// directory. This ensures that the storage object is always associated with a valid file system
        /// location.</remarks>
        /// <param name="fileNameOrPath">The file name or full path to associate with this storage object. Cannot be null or empty.</param>
        public StorageObject(string fileNameOrPath)
        {
            FullPath = GetValidPath(fileNameOrPath);

            // Ensure the root path exists.
            if (!Directory.Exists(ParentDirectory))
            {
                Directory.CreateDirectory(ParentDirectory);
            }

            // Initialize event listners
            StorageChanged += StorageObject_StorageChanged;

            // Load the information
            _informationInternal = CrateNewInformationInstance()!;
        }

        #region Properties

        /// <summary>
        /// Gets the name of the storage object, typically the file or directory name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the full path of the parent directory for the current file or directory.
        /// </summary>
        public string ParentDirectory => GetDirectoryPath(FullPath);

        /// <summary>
        /// Gets the full path of the file or directory by combining the parent directory and the name.
        /// </summary>
        public string FullPath { get; protected set; }

        #endregion

        /// <summary>
        /// Occurs when the storage content changes.
        /// </summary>
        /// <remarks>Subscribe to this event to be notified when items are added, removed, or updated in
        /// the storage. The event provides details about the change through the <see cref="StorageChangedEventArgs"/>
        /// parameter.</remarks>
        public event EventHandler<StorageChangedEventArgs> StorageChanged;

        #region Methods
        /// <summary>
        /// Asynchronously copies the current item to the specified destination.
        /// </summary>
        /// <remarks>If the destination already exists and <paramref name="overwrite"/> is <see
        /// langword="false"/>,  the operation will fail. The method supports cancellation via the <paramref
        /// name="cancellationToken"/>  parameter, and progress reporting if a <paramref name="progress"/> instance is
        /// provided.</remarks>
        /// <param name="destination">The path to the destination where the item will be copied.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination if it already exists.  <see langword="true"/> to
        /// overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="maxConcurrent">The maximum number of concurrent operations allowed during the copy process.  Must be greater than zero. The
        /// default is 4.</param>
        /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report progress updates during the copy operation. The
        /// progress value represents the number of bytes copied so far.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous copy operation.</returns>
        public abstract Task CopyAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously moves the current item to the specified destination.
        /// </summary>
        /// <remarks>This method performs the move operation asynchronously, allowing other tasks to run
        /// concurrently.  If <paramref name="overwrite"/> is <see langword="false"/> and the destination already
        /// exists,  the operation will fail. The <paramref name="maxConcurrent"/> parameter can be adjusted to optimize
        /// performance based on the environment and workload.</remarks>
        /// <param name="destination">The path to the destination where the item will be moved. This must be a valid and accessible path.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination if it already exists.  <see langword="true"/> to
        /// overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="maxConcurrent">The maximum number of concurrent operations allowed during the move. Must be a positive integer.  The
        /// default value is 4.</param>
        /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report progress updates during the move operation.  The
        /// progress value represents the percentage of the operation completed.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the operation to complete.  The operation
        /// will be canceled if the token is triggered.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous move operation.</returns>
        public abstract Task MoveAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the current resource asynchronously.
        /// </summary>
        /// <remarks>This method performs an asynchronous operation to delete the resource represented by
        /// the current instance.  Ensure that the resource is no longer needed before calling this method, as the
        /// deletion is irreversible.</remarks>
        /// <returns>A <see cref="Task"/> that represents the asynchronous delete operation.</returns>
        public abstract Task DeleteAsync();

        /// <summary>
        /// Renames the current entity to the specified new name asynchronously.
        /// </summary>
        /// <param name="newName">The new name to assign to the entity. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous rename operation.</returns>
        public abstract Task RenameAsync(string newName);

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_informationInternal is null)
            {
                return $"{Name} | Info: N/A";
            }

            return _informationInternal.ToString()!;
        }

        /// <summary>
        /// Calculates an appropriate buffer size based on the specified total size, ensuring the result is within
        /// allowed minimum and maximum limits.
        /// </summary>
        /// <remarks>This method aims to divide the total size into approximately 100 chunks, but will not
        /// return a value smaller than the minimum buffer size or larger than the maximum buffer size.</remarks>
        /// <param name="size">The total size, in bytes, for which to determine a suitable buffer size. If null or zero, the minimum buffer
        /// size is used.</param>
        /// <returns>A buffer size, in bytes, that is suitable for the specified total size and constrained to the allowed
        /// minimum and maximum values.</returns>
        protected static uint GetBufferSize(ulong? size)
        {
            if (size is null || size == 0)
                return MIN_BUFFER_SIZE;
            else if (size < MIN_BUFFER_SIZE)
                return MIN_BUFFER_SIZE;
            else if (size > MAX_BUFFER_SIZE)
                return MAX_BUFFER_SIZE;
            else
                return Math.Min(MAX_BUFFER_SIZE, Math.Max(MIN_BUFFER_SIZE, (uint)(size / 100))); // Aim for ~100 chunks
        }

        /// <summary>
        /// Validates the specified path and returns its absolute form if it meets the required criteria for the given
        /// assertion type.
        /// </summary>
        /// <remarks>Use this method to ensure that a path is valid and appropriate for the intended file
        /// or directory operation before proceeding. The method does not check for the existence of the file or
        /// directory unless it is necessary to disallow an existing file or directory based on the assertion
        /// type.</remarks>
        /// <param name="path">The file system path to validate. Cannot be null, empty, or consist only of white-space characters, and must
        /// not contain invalid path characters.</param>
        /// <returns>The absolute path corresponding to the specified path if it passes validation.</returns>
        /// <exception cref="ArgumentException">Thrown if the path is null, empty, consists only of white-space characters, contains invalid path
        /// characters, points to an existing directory when a file is expected, points to an existing file when a
        /// directory is expected, or if an invalid assertion type is specified.</exception>
        protected virtual string GetValidPath(string path)
        {
            // Method 1: Check if the path is null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The path cannot be null, empty, or whitespace.", nameof(path));
            }

            // Method 2: Verify the path does not contain invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException("The path contains invalid characters.", nameof(path));
            }

            var fullPath = Path.GetFullPath(path);

            return fullPath;
        }

        /// <summary>
        /// Creates a new instance of a storage information object.
        /// </summary>
        /// <remarks>Derived classes must implement this method to provide a concrete instance of <see
        /// cref="IStorageInformation"/> appropriate for their storage mechanism.</remarks>
        /// <returns>An <see cref="IStorageInformation"/> instance representing the newly created storage information.</returns>
        protected abstract IStorageInformation CrateNewInformationInstance();

        /// <summary>
        /// Raises the StorageChanged event to notify subscribers of changes in the storage state.
        /// </summary>
        /// <remarks>Override this method in a derived class to provide custom handling when the storage
        /// changes. This method is typically called when the storage state is updated and should not be called directly
        /// by user code.</remarks>
        /// <param name="e">An object that contains the event data for the storage change.</param>
        protected virtual void OnStorageChanged(StorageChangedEventArgs e)
        {
            StorageChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Handles changes to the underlying storage object by updating the object's path and information as needed.
        /// </summary>
        /// <remarks>This method updates the object's identity when the storage path changes and refreshes
        /// cached information asynchronously when the storage is created or modified. Derived classes can override this
        /// method to customize how storage changes are handled.</remarks>
        /// <param name="sender">The source of the storage change event. This is typically the storage object that triggered the event.</param>
        /// <param name="e">An event argument containing details about the storage change, including the absolute path and the type of
        /// change.</param>
        protected virtual void StorageObject_StorageChanged(object? sender, StorageChangedEventArgs e)
        {
            // Changiing the path will change the entrie identity of the object.
            FullPath = GetValidPath(e.AbsolutePath);

            if (e.ChangeType == StorageChangeType.Created ||
                e.ChangeType == StorageChangeType.Modified)
            {
                // Update the information asynchronously
                Task.Run(() =>
                {
                    lock (_infoLock)
                    {
                        // Load the new information
                        _informationInternal = CrateNewInformationInstance();
                    }
                });
            }
        }

        /// <summary>
        /// Determines if two paths are on the same volume
        /// </summary>
        /// <param name="sourcepath"></param>
        /// <param name="destpath"></param>
        /// <returns></returns>
        protected static bool AreOnSameVolume(string sourcepath, string destpath)
        {
            try
            {
                var drive1 = Path.GetPathRoot(Path.GetFullPath(sourcepath));
                var drive2 = Path.GetPathRoot(Path.GetFullPath(destpath));

                return string.Equals(drive1, drive2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If we can't determine, assume different volumes
                return false;
            }
        }

        /// <summary>
        /// Ensures that the specified destination path refers to an existing directory, creating it if necessary.
        /// </summary>
        /// <remarks>If the specified destination path does not exist, the method creates the directory.
        /// If the path refers to an existing file, an exception is thrown. Derived classes may override this method to
        /// implement custom behavior, such as handling the overwrite parameter.</remarks>
        /// <param name="destination">The destination path to process. This parameter is updated to contain the absolute path of the directory.</param>
        /// <param name="overwrite">A value indicating whether existing contents at the destination should be overwritten. This parameter is
        /// reserved for derived implementations and is not used in the base method.</param>
        /// <exception cref="InvalidOperationException">Thrown if the destination path refers to an existing file instead of a directory, or if the directory cannot
        /// be created or accessed.</exception>
        protected virtual void ProcessDestinationPath(ref string destination, bool overwrite = false)
        {
            try
            {
                // Verify the destination path. The destination provided should be a directory.
                destination = Path.GetFullPath(destination);

                // Check if destination points to an existing file (not a directory)
                if (File.Exists(destination))
                {
                    throw new InvalidOperationException($"The destination '{destination}' is an existing file, not a directory.");
                }

                // Create the directory if it doesn't exist
                if (!Directory.Exists(destination))
                {
                    Directory.CreateDirectory(destination);
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw business logic exceptions as-is
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or IOException or UnauthorizedAccessException)
            {
                // Wrap filesystem exceptions with context
                throw new InvalidOperationException($"Failed to process destination path '{destination}'.", ex);
            }
        }

        /// <summary>
        /// Gets the directory of a path-like string.
        /// </summary>
        /// <param name="pathlike"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static string GetDirectoryPath(string pathlike)
        {
            string? dir;

            try
            {
                dir = Path.GetFullPath(pathlike);

                do
                {
                    dir = Path.GetDirectoryName(dir);

                    if (!string.IsNullOrEmpty(dir))
                    {
                        return dir;
                    }
                }
                while (dir is not null);

                throw new ArgumentException($"The path '{pathlike}' does not contain a valid directory.");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"The path '{pathlike}' is not a valid path.", ex);
            }
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            // Clean up event listeners
            StorageChanged -= StorageObject_StorageChanged;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
