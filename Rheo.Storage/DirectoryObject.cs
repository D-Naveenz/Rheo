using Rheo.Storage.Contracts;
using Rheo.Storage.Core;
using Rheo.Storage.Information;
using System.Collections.Concurrent;

namespace Rheo.Storage
{
    /// <summary>
    /// Represents a directory in the file system and provides methods for accessing, monitoring, and manipulating its
    /// contents.
    /// </summary>
    /// <remarks>A DirectoryObject encapsulates a directory path and enables operations such as retrieving
    /// files and subdirectories, accessing specific files or directories by relative path, and performing copy, move,
    /// rename, and delete actions. Upon instantiation, the DirectoryObject automatically monitors the represented
    /// directory and its subdirectories for changes, such as file or directory creation, deletion, and attribute
    /// modifications. This monitoring may affect properties that reflect the current state of the directory. The class
    /// provides both synchronous and asynchronous methods for common directory operations, and ensures that resource
    /// management and error handling are consistent with .NET best practices.</remarks>
    public class DirectoryObject : DirectoryHandler, IDirectoryObject
    {
        /// <summary>
        /// The default interval, in milliseconds, used to debounce file system watcher events when monitoring directory changes.
        /// </summary>
        public const int DefaultWatchInterval = 500; // milliseconds

        private readonly ConcurrentBag<string> _changedFiles = [];
        private readonly FileSystemWatcher _watcher;
        private Timer? _debounceTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryObject"/> class to monitor the specified directory for changes,
        /// using a default watch interval of 500 milliseconds.
        /// </summary>
        /// <param name="path">The full path of the directory to monitor. Cannot be null or empty.</param>
        public DirectoryObject(string path) : this(path, DefaultWatchInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DirectoryObject class and begins monitoring the specified directory for
        /// file changes.
        /// </summary>
        /// <remarks>The directory is monitored for file creation, deletion, and modification events,
        /// including changes in subdirectories. The watch interval acts as a debounce period to reduce redundant
        /// processing when multiple file system events occur in quick succession.</remarks>
        /// <param name="path">The full path of the directory to monitor. Cannot be null or empty.</param>
        /// <param name="watchInterval">The interval, in milliseconds, to wait after the last detected file system event before processing changes.
        /// Must be greater than zero.</param>
        /// <exception cref="IOException">Thrown if the directory watcher cannot be initialized for the specified path.</exception>
        public DirectoryObject(string path, int watchInterval) : base(path)
        {
            try
            {
                _watcher = new FileSystemWatcher(FullPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.Size 
                                 | NotifyFilters.LastWrite
                };

                // Event handlers
                // We need a file system watcher to monitor changes in the directory.
                // But we only need to moniter if only affects the directory properties (counts, size, etc.)
                _watcher.Changed += Watcher_Changed;
                _watcher.Created += Watcher_Changed;
                _watcher.Deleted += Watcher_Changed;

                _watcher.EnableRaisingEvents = true;

                // Debounce timer: waits <watchInterval> milliseconds after last event before processing
                _debounceTimer = new Timer(OnDebounceTimerTick, _changedFiles, Timeout.Infinite, watchInterval);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to initialize FileSystemWatcher for directory at path: {FullPath}", ex);
            }
        }

        internal DirectoryObject(DirectoryInformation information): base(information)
        {
            try
            {
                _watcher = new FileSystemWatcher(information.AbsolutePath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.Size
                                 | NotifyFilters.LastWrite
                };

                // Event handlers
                // We need a file system watcher to monitor changes in the directory.
                // But we only need to moniter if only affects the directory properties (counts, size, etc.)
                _watcher.Changed += Watcher_Changed;
                _watcher.Created += Watcher_Changed;
                _watcher.Deleted += Watcher_Changed;

                _watcher.EnableRaisingEvents = true;

                // Debounce timer: waits <watchInterval> milliseconds after last event before processing
                _debounceTimer = new Timer(OnDebounceTimerTick, _changedFiles, Timeout.Infinite, DefaultWatchInterval);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to initialize FileSystemWatcher for directory at path: {information.AbsolutePath}", ex);
            }
        }

        /// <inheritdoc/>
        public override string Name
        {
            get
            {
                lock(StateLock)
                {
                    return Path.GetFileName(FullPath)!;
                }
            }
        }

        /// <inheritdoc/>
        public new DirectoryInformation Information => base.Information as DirectoryInformation ?? throw new InvalidOperationException("Information is not of type DirectoryInformation.");

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IFileObject GetFile(string relativePath)
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

            return new FileObject(fullPath);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IDirectoryObject GetDirectory(string relativePath)
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
            return new DirectoryObject(fullPath);
        }

        /// <inheritdoc/>
        public IDirectoryObject Copy(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            var info = CopyInternal(destination, overwrite);
            return new DirectoryObject(info);
        }

        /// <inheritdoc/>
        public IDirectoryObject Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            var info = CopyInternal(destination, overwrite, progress);
            return new DirectoryObject(info);
        }

        /// <inheritdoc/>
        public async Task<IDirectoryObject> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var info = await CopyInternalAsync(destination, overwrite, null, cancellationToken);
            return new DirectoryObject(info);
        }

        /// <inheritdoc/>
        public async Task<IDirectoryObject> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var info = await CopyInternalAsync(destination, overwrite, progress, cancellationToken);
            return new DirectoryObject(info);
        }

        /// <inheritdoc/>
        public void Delete()
        {
            ThrowIfDisposed();
            DeleteInternal();
        }

        /// <inheritdoc/>
        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DeleteInternalAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public void Move(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            MoveInternal(destination, overwrite);
        }

        /// <inheritdoc/>
        public void Move(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            MoveInternal(destination, overwrite, progress);
        }

        /// <inheritdoc/>
        public Task MoveAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return MoveInternalAsync(destination, overwrite, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task MoveAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return MoveInternalAsync(destination, overwrite, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public void Rename(string newName)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Rename already locks
            RenameInternal(newName);
        }

        /// <inheritdoc/>
        public async Task RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Rename already locks
            await RenameInternalAsync(newName, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            Timer? timerToDispose = null;
            ManualResetEvent? waitHandle = null;
            
            lock (StateLock)
            {
                // Check if already disposed
                try { ThrowIfDisposed(); }
                catch (ObjectDisposedException) { return; }

                // Disable watcher events before disposing
                if (_watcher != null)
                {
                    _watcher.Changed -= Watcher_Changed;
                    _watcher.Created -= Watcher_Changed;
                    _watcher.Deleted -= Watcher_Changed;
                }

                // Stop timer and prepare for disposal (don't wait inside lock)
                if (_debounceTimer != null)
                {
                    _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    timerToDispose = _debounceTimer;
                    waitHandle = new ManualResetEvent(false);
                    _debounceTimer = null; // Prevent timer from being reused
                }
                
                // Dispose watcher
                _watcher?.Dispose();
            }
            
            // Wait for timer outside the lock to avoid deadlock
            if (timerToDispose != null && waitHandle != null)
            {
                timerToDispose.Dispose(waitHandle);
                waitHandle.WaitOne();
                waitHandle.Dispose();
            }

            // Call base dispose (handles its own locking)
            base.Dispose();
            
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Validates the specified path and returns its full directory path if it does not point to an existing file.
        /// </summary>
        /// <param name="path">The path to validate. Must not refer to an existing file.</param>
        /// <returns>The full directory path corresponding to the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified path points to an existing file.</exception>
        protected override string GetValidPath(string path)
        {
            var fullPath = base.GetValidPath(path);

            // Check the path is point to an existing file. If yes, the path should be invalid
            if (File.Exists(fullPath))
            {
                throw new ArgumentException(
                    "The specified path points to an existing file. Please provide a valid directory path, not a file.",
                    nameof(path)
                );
            }

            return fullPath;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Add the changed file to the collection
            _changedFiles.Add(e.FullPath);

            // Reset the debounce timer
            _debounceTimer?.Change(2000, Timeout.Infinite);
        }

        private void OnDebounceTimerTick(object? state)
        {
            var changedFiles = (ConcurrentBag<string>)state!;
            if (!changedFiles.IsEmpty)
            {
                // Check if directory still exists before creating DirectoryInformation
                if (!Directory.Exists(FullPath))
                {
                    return; // Directory was deleted, skip this callback
                }
                
                var newObject = new DirectoryInformation(FullPath);
                RaiseChanged(StorageChangeType.Modified, newObject);
                changedFiles.Clear();
            }
        }
    }
}
