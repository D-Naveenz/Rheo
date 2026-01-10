using Rheo.Storage.Contracts;

namespace Rheo.Storage
{
    /// <summary>
    /// Provides an abstract base class for file system storage objects, supporting common operations such as copy,
    /// move, delete, and rename, along with access to storage metadata.
    /// </summary>
    /// <remarks>This class defines a common interface and shared logic for file and directory storage
    /// objects. It enforces path validation and ensures that the associated file system location exists upon
    /// instantiation. Derived classes should implement the abstract members to provide specific behavior for different
    /// storage types. Thread safety for operations affecting the same path is supported via internal synchronization
    /// mechanisms.</remarks>
    /// <typeparam name="TObj">The type that implements the storage object, used for fluent return types in derived classes.</typeparam>
    /// <typeparam name="TInfo">The type that provides metadata information about the storage object, such as size, attributes, and timestamps.</typeparam>
    public abstract class StorageObject<TObj, TInfo> : IDisposable      // Using Curiously Recurring Template Pattern (CRTP)
        where TObj : StorageObject<TObj, TInfo>
        where TInfo : IStorageInformation
    {
        private const int MIN_BUFFER_SIZE = 1024; // 1KB
        private const int MAX_BUFFER_SIZE = 16 * 1024 * 1024; // 16MB

        //private static readonly Dictionary<string, SemaphoreSlim> _handlingLocks = [];
        //private static readonly Lock _dictionaryLock = new();
        
        private bool _disposed;
        private readonly SemaphoreSlim _stateLockingSemaphore = new(1, 1);
        private readonly Lock _stateLock = new();

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

            // Initialize the information property
            Information = TInfo.Create<TInfo>(FullPath);
        }

        #region Properties
        /// <summary>
        /// Gets metadata information about the storage object, such as size, attributes, and timestamps.
        /// </summary>
        public TInfo? Information { get; private set; }

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

        internal SemaphoreSlim Semaphore => _stateLockingSemaphore;

        internal Lock Lock => _stateLock;

        #endregion

        #region Handling Methods
        /// <summary>
        /// Copies the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be copied.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <returns>A new instance of <typeparamref name="TObj"/> representing the copied object.</returns>
        public abstract TObj Copy(string destination, bool overwrite);

        /// <summary>
        /// Copies the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be copied.</param>
        /// <param name="progress">An optional progress reporter for copy progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <returns>A new instance of <typeparamref name="TObj"/> representing the copied object.</returns>
        public abstract TObj Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false);

        /// <summary>
        /// Asynchronously copies the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be copied.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous copy operation. The result is a new instance of <typeparamref name="TObj"/> representing the copied object.</returns>
        public abstract Task<TObj> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously copies the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be copied.</param>
        /// <param name="progress">An optional progress reporter for copy progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous copy operation. The result is a new instance of <typeparamref name="TObj"/> representing the copied object.</returns>
        public abstract Task<TObj> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the storage object from the file system.
        /// </summary>
        public abstract void Delete();

        /// <summary>
        /// Asynchronously deletes the storage object from the file system.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        public abstract Task DeleteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <returns>A new instance of <typeparamref name="TObj"/> representing the moved object.</returns>
        public abstract TObj Move(string destination, bool overwrite);

        /// <summary>
        /// Moves the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="progress">An optional progress reporter for move progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <returns>A new instance of <typeparamref name="TObj"/> representing the moved object.</returns>
        public abstract TObj Move(string destination, IProgress<StorageProgress>? progress, bool overwrite = false);

        /// <summary>
        /// Asynchronously moves the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous move operation. The result is a new instance of <typeparamref name="TObj"/> representing the moved object.</returns>
        public abstract Task<TObj> MoveAsync(string destination, bool overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously moves the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="progress">An optional progress reporter for move progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous move operation. The result is a new instance of <typeparamref name="TObj"/> representing the moved object.</returns>
        public abstract Task<TObj> MoveAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renames the current object to the specified name.
        /// </summary>
        /// <param name="newName">The new name to assign to the object. Cannot be null or empty.</param>
        public abstract void Rename(string newName);

        /// <summary>
        /// Asynchronously renames the current item to the specified name.
        /// </summary>
        /// <param name="newName">The new name to assign to the item. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the rename operation.</param>
        /// <returns>A task that represents the asynchronous rename operation.</returns>
        public abstract Task RenameAsync(string newName, CancellationToken cancellationToken = default);

        #endregion

        /// <summary>
        /// Copies the information and full path from the specified source object to the current instance.
        /// </summary>
        /// <param name="source">The source <see cref="StorageObject{TObj, TInfo}"/> from which to copy information and full path. Cannot be
        /// null.</param>
        public virtual void CopyFrom(StorageObject<TObj, TInfo> source)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(source);

            lock (_stateLock)  // Protect state mutation
            {
                Information = source.Information;
                FullPath = source.FullPath;
            }
        }

        /// <summary>
        /// Calculates the recommended buffer size based on the current storage information.
        /// </summary>
        /// <remarks>The returned buffer size is determined by the size of the underlying storage. If the
        /// storage size is zero or less than the minimum buffer size, the minimum buffer size is returned. If the
        /// storage size exceeds the maximum buffer size, the maximum buffer size is returned. Otherwise, the buffer
        /// size is calculated to target approximately 100 chunks, within the allowed range.</remarks>
        /// <returns>An integer representing the recommended buffer size, constrained between the minimum and maximum allowed
        /// values.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the storage information has not been initialized.</exception>
        public int GetBufferSize()
        {
            if (Information is null)
            {
                throw new InvalidOperationException("Storage information is not initialized.");
            }
            var size = Information.Size;

            if (size == 0)
                return MIN_BUFFER_SIZE;
            else if (size < MIN_BUFFER_SIZE)
                return MIN_BUFFER_SIZE;
            else if (size > MAX_BUFFER_SIZE)
                return MAX_BUFFER_SIZE;
            else
                return Math.Min(MAX_BUFFER_SIZE, Math.Max(MIN_BUFFER_SIZE, (int)(size / 100))); // Aim for ~100 chunks
        }

        /// <summary>
        /// Releases all resources used by the current instance of the class.
        /// </summary>
        /// <remarks>Call this method when you are finished using the object to release any resources it
        /// is holding. After calling Dispose, the object should not be used. This method can be overridden in a derived
        /// class to release additional resources.</remarks>
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                // Clean up managed resources here, if any
                FullPath = string.Empty;
                Information = default;

                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this storage object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object has already been disposed.</exception>
        public void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Information is null)
            {
                return $"{Name} | Info: N/A";
            }

            return Information.ToString()!;
        }

        /// <summary>
        /// Determines whether the specified destination path is located on the same root (drive or volume) as the
        /// current path.
        /// </summary>
        /// <remarks>If the root of either path cannot be determined (for example, if the path is
        /// invalid), the method returns false.</remarks>
        /// <param name="destpath">The destination path to compare with the current path. Can be a relative or absolute path.</param>
        /// <returns>true if the destination path is on the same root as the current path; otherwise, false.</returns>
        internal bool IsInTheSameRoot(string destpath)
        {
            try
            {
                var drive1 = Path.GetPathRoot(Path.GetFullPath(FullPath));
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
    }
}
