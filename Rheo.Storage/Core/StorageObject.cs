using Rheo.Storage.Interop;
using Rheo.Storage.Contracts;

namespace Rheo.Storage.Core
{
    /// <summary>
    /// Provides an abstract base class for file system storage objects, encapsulating common functionality such as path
    /// validation, resource management, and change notification.
    /// </summary>
    /// <remarks>The StorageObject class serves as a foundation for implementing file- or directory-based
    /// storage abstractions. It manages the association with a file system path, ensures the existence of parent
    /// directories, and provides thread-safe state management. Derived classes should implement the required members to
    /// provide specific storage behaviors. The class supports change notification through the Changed event and
    /// implements resource cleanup via IDisposable. Instances should not be used after they have been
    /// disposed.</remarks>
    public abstract class StorageObject : IStorageObject    // Using Curiously Recurring Template Pattern (CRTP)
    {
        private const int MIN_BUFFER_SIZE = 1024; // 1KB
        private const int MAX_BUFFER_SIZE = 16 * 1024 * 1024; // 16MB

        private readonly SemaphoreSlim _stateLockingSemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the StorageObject class with the specified file system path, optionally
        /// validating and creating the parent directory if necessary.
        /// </summary>
        /// <remarks>If validated is false, the constructor validates the provided path and creates the
        /// parent directory if it does not already exist. This ensures that the storage object is always associated
        /// with a valid and accessible file system location.</remarks>
        /// <param name="path">The file system path to associate with the storage object. This can be either a validated or unvalidated
        /// path, depending on the value of the validated parameter.</param>
        /// <param name="validated">true to indicate that the path has already been validated; false to validate the path and ensure the parent
        /// directory exists before use.</param>
        public StorageObject(string path, bool validated)
        {
            if (validated)
            {
                FullPath = path;
            }
            else
            {
                FullPath = GetValidPath(path);

                // Ensure the root path exists.
                if (!Directory.Exists(ParentDirectory))
                {
                    Directory.CreateDirectory(ParentDirectory);
                }
            }

            // Subscribe to change events
            Changed += OnStateChanged;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the class.
        /// </summary>
        /// <remarks>Call this method when you are finished using the object to release any resources it
        /// is holding. After calling Dispose, the object should not be used. This method can be overridden in a derived
        /// class to release additional resources.</remarks>
        public virtual void Dispose()
        {
            lock (StateLock)
            {
                if (!_disposed)
                {
                    // Unsubscribe from events
                    Changed -= OnStateChanged;

                    // Clean up managed resources here, if any
                    FullPath = string.Empty;
                    Information = default!;
                    _disposed = true;
                }
            }

            GC.SuppressFinalize(this);
        }

        #region Properties
        /// <inheritdoc/>
        public abstract IStorageInformation Information { get; protected set; }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public string ParentDirectory => GetDirectoryPath(FullPath);

        /// <inheritdoc/>
        public string FullPath { get; protected set; }

        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Gets the internal lock object used to synchronize access to the state of this storage object.
        /// </summary>
        public Lock StateLock { get; } = new();

        internal SemaphoreSlim Semaphore => _stateLockingSemaphore;

        #endregion

        /// <inheritdoc/>
        public event EventHandler<StorageChangedEventArgs>? Changed;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
        /// Asynchronously waits until the specified file becomes available for exclusive read/write access, or until
        /// the operation times out.
        /// </summary>
        /// <remarks>This method repeatedly attempts to open the file with exclusive access to determine
        /// its availability. If the file does not exist or remains locked by another process, the method waits and
        /// retries until the timeout is reached or the operation is canceled. The method is thread-safe and can be used
        /// to coordinate access to files shared between processes.</remarks>
        /// <param name="filePath">The full path of the file to check for availability. Cannot be null or empty.</param>
        /// <param name="timeoutMilliseconds">The maximum time, in milliseconds, to wait for the file to become available. Specify 0 or a negative value
        /// to wait indefinitely. The default is 10,000 milliseconds (10 seconds).</param>
        /// <param name="checkIntervalMilliseconds">The interval, in milliseconds, between successive checks for file availability. The default is 200
        /// milliseconds.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the wait operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the file
        /// became available within the specified timeout; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        protected static async Task<bool> WaitForFileAvailableAsync(string filePath, int timeoutMilliseconds = 10000, int checkIntervalMilliseconds = 200, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            var startTime = DateTime.UtcNow;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // First, check if file exists
                if (!File.Exists(filePath))
                {
                    // File doesn't exist yet, keep waiting
                    goto CheckTimeout;
                }

                try
                {
                    // Try opening with exclusive access
                    using FileStream fs = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    // Successfully opened — file is unlocked and available
                    return true;
                }
                catch (IOException)
                {
                    // File is still locked by another process
                }
                catch (UnauthorizedAccessException)
                {
                    // File might be read-only or locked by OS
                }

            CheckTimeout:
                // Check timeout
                if (timeoutMilliseconds > 0 &&
                    (DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMilliseconds)
                {
                    return false; // Timed out
                }

                await Task.Delay(checkIntervalMilliseconds, cancellationToken);
            }
        }

        /// <summary>
        /// Waits for a file to become available for access within a specified timeout period.
        /// </summary>
        /// <remarks>This method blocks the calling thread until the file is available or the timeout
        /// elapses. Use this method when synchronous waiting is required. For asynchronous scenarios, use
        /// WaitForFileAvailableAsync.</remarks>
        /// <param name="filePath">The full path of the file to check for availability. Cannot be null or empty.</param>
        /// <param name="timeoutMilliseconds">The maximum amount of time, in milliseconds, to wait for the file to become available. Must be greater than
        /// zero. The default is 10,000 milliseconds.</param>
        /// <param name="checkIntervalMilliseconds">The interval, in milliseconds, between availability checks. Must be greater than zero. The default is 200
        /// milliseconds.</param>
        /// <returns>true if the file becomes available within the specified timeout period; otherwise, false.</returns>
        protected static bool WaitForFileAvailable(string filePath, int timeoutMilliseconds = 10000, int checkIntervalMilliseconds = 200)
        {
            return WaitForFileAvailableAsync(filePath, timeoutMilliseconds, checkIntervalMilliseconds, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Asynchronously waits until the specified directory becomes available for access or until the operation times
        /// out.
        /// </summary>
        /// <remarks>This method checks for both the existence of the directory and the ability to access
        /// it. On Windows, it attempts to open a handle to the directory; on other platforms, it tries to enumerate
        /// files within the directory. The method periodically checks availability until the directory is accessible,
        /// the timeout elapses, or the operation is canceled.</remarks>
        /// <param name="dirPath">The full path of the directory to check for availability. Cannot be null or empty.</param>
        /// <param name="timeoutMilliseconds">The maximum time, in milliseconds, to wait for the directory to become available. Specify 0 for an infinite
        /// timeout. The default is 10,000 milliseconds.</param>
        /// <param name="checkIntervalMilliseconds">The interval, in milliseconds, between successive checks for directory availability. The default is 200
        /// milliseconds.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the wait operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
        /// directory became available within the specified timeout; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dirPath"/> is null or empty.</exception>
        protected static async Task<bool> WaitForDirectoryAvailableAsync(string dirPath, int timeoutMilliseconds = 10000, int checkIntervalMilliseconds = 200, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
                throw new ArgumentException("Directory path cannot be null or empty.", nameof(dirPath));

            var startTime = DateTime.UtcNow;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // First, check if directory exists
                if (!Directory.Exists(dirPath))
                {
                    // Directory doesn't exist yet, keep waiting
                    goto CheckTimeout;
                }

                if (OperatingSystem.IsWindows())
                {
                    // On Windows, try to open a handle to the directory
                    using var handle = Win32.CreateFile(
                        dirPath,
                        Win32.GENERIC_READ,
                        Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE | Win32.FILE_SHARE_DELETE,
                        IntPtr.Zero,
                        Win32.OPEN_EXISTING,
                        Win32.FILE_FLAG_BACKUP_SEMANTICS,    // Required for directories
                        IntPtr.Zero);

                    // If we got a valid handle, the directory is available
                    if (!handle.IsInvalid)
                        return true;
                }
                else
                {
                    // On non-Windows systems, try to enumerate files to test access
                    try
                    {
                        _ = Directory.GetFiles(dirPath);
                        return true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Access denied, continue waiting
                    }
                    catch (IOException)
                    {
                        // Directory is busy, continue waiting
                    }
                }

            CheckTimeout:
                // Check timeout
                if (timeoutMilliseconds > 0 &&
                    (DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMilliseconds)
                {
                    return false; // Timed out
                }

                await Task.Delay(checkIntervalMilliseconds, cancellationToken);
            }
        }

        /// <summary>
        /// Waits for the specified directory to become available within a given timeout period.
        /// </summary>
        /// <remarks>This method blocks the calling thread until the directory is available or the timeout
        /// elapses. Use this method when synchronous waiting is required; for asynchronous scenarios, use
        /// WaitForDirectoryAvailableAsync.</remarks>
        /// <param name="dirPath">The full path of the directory to check for availability. Cannot be null or empty.</param>
        /// <param name="timeoutMilliseconds">The maximum amount of time, in milliseconds, to wait for the directory to become available. Must be greater
        /// than zero. The default is 10,000 milliseconds.</param>
        /// <param name="checkIntervalMilliseconds">The interval, in milliseconds, between availability checks. Must be greater than zero. The default is 200
        /// milliseconds.</param>
        /// <returns>true if the directory becomes available within the specified timeout period; otherwise, false.</returns>
        protected static bool WaitForDirectoryAvailable(string dirPath, int timeoutMilliseconds = 10000, int checkIntervalMilliseconds = 200)
        {
            return WaitForDirectoryAvailableAsync(dirPath, timeoutMilliseconds, checkIntervalMilliseconds, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

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

        internal void RaiseChanged(StorageChangeType changeType, IStorageInformation? newInformation)
        {
            Changed?.Invoke(this, new StorageChangedEventArgs(changeType, newInformation));
        }

        private void OnStateChanged(object? sender, StorageChangedEventArgs e)
        {
            ThrowIfDisposed();

            if (e.ChangeType != StorageChangeType.Deleted && e.NewInfo is not null)
            {
                lock (StateLock)
                {
                    // Update information and path on modification or relocation
                    Information = e.NewInfo;
                    FullPath = Information.AbsolutePath;
                }
            }
            else if (e.ChangeType == StorageChangeType.Deleted)
            {
                // Dispose the object if it has been deleted
                Dispose();
            }
        }

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
