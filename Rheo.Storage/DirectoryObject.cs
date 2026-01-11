using Rheo.Storage.Handling;
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
    public class DirectoryObject : StorageObject<DirectoryObject, DirectoryInformation>
    {
        /// <summary>
        /// The default interval, in milliseconds, used to debounce file system watcher events when monitoring directory changes.
        /// </summary>
        public const int DefaultWatchInterval = 500; // milliseconds

        private readonly ConcurrentBag<string> _changedFiles = [];
        private readonly Timer? _debounceTimer;
        private readonly FileSystemWatcher _watcher;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryObject"/> class to monitor the specified directory for changes,
        /// using a default watch interval of 500 milliseconds.
        /// </summary>
        /// <param name="path">The full path of the directory to monitor. Cannot be null or empty.</param>
        public DirectoryObject(string path) : this(path, DefaultWatchInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DirectoryObject class to monitor the specified directory for changes.
        /// </summary>
        /// <remarks>The directory is monitored for file name, size, and last write time changes,
        /// including changes in subdirectories. The watch interval is used to debounce rapid sequences of file system
        /// events, reducing redundant processing.</remarks>
        /// <param name="path">The full path of the directory to monitor. Cannot be null or empty.</param>
        /// <param name="watchInterval">The interval, in milliseconds, to wait after the last detected change before processing events. Must be
        /// greater than zero.</param>
        /// <exception cref="IOException">Thrown if the directory watcher cannot be initialized for the specified path.</exception>
        public DirectoryObject(string path, int watchInterval) : base(path)
        {
            path = FullPath; // Ensure base class has processed the path

            // Ensure the Directory exists
            Directory.CreateDirectory(path);

            try
            {
                _watcher = new FileSystemWatcher(path)
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
                throw new IOException($"Failed to initialize FileSystemWatcher for directory at path: {path}", ex);
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
        /// <returns>A <see cref="FileObject"/> instance representing the file at the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is an absolute path.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist at the specified relative path.</exception>
        public FileObject GetFile(string relativePath)
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
        /// Retrieves a <see cref="DirectoryObject"/> representing the directory at the specified relative path.
        /// </summary>
        /// <remarks>This method combines the specified <paramref name="relativePath"/> with the base
        /// directory's full path to locate the target directory. Ensure that the relative path is valid and points to
        /// an existing directory.</remarks>
        /// <param name="relativePath">The relative path to the directory, relative to the base directory represented by this instance. The path
        /// must not be rooted.</param>
        /// <returns>A <see cref="DirectoryObject"/> representing the directory at the specified relative path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is a rooted path.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by <paramref name="relativePath"/> does not exist.</exception>
        public DirectoryObject GetDirectory(string relativePath)
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
        public override DirectoryObject Copy(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            return DirectoryHandling.Copy(this, destination, overwrite);
        }

        /// <inheritdoc/>
        public override DirectoryObject Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            return DirectoryHandling.Copy(this, destination, overwrite, progress);
        }

        /// <inheritdoc/>
        public override Task<DirectoryObject> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DirectoryHandling.CopyAsync(this, destination, overwrite, null, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<DirectoryObject> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DirectoryHandling.CopyAsync(this, destination, overwrite, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Delete()
        {
            ThrowIfDisposed();
            DirectoryHandling.Delete(this);
        }

        /// <inheritdoc/>
        public override Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DirectoryHandling.DeleteAsync(this, cancellationToken);
        }

        /// <inheritdoc/>
        public override DirectoryObject Move(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            return DirectoryHandling.Move(this, destination, overwrite);
        }

        /// <inheritdoc/>
        public override DirectoryObject Move(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            return DirectoryHandling.Move(this, destination, overwrite, progress);
        }

        /// <inheritdoc/>
        public override Task<DirectoryObject> MoveAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DirectoryHandling.MoveAsync(this, destination, overwrite, null, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<DirectoryObject> MoveAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return DirectoryHandling.MoveAsync(this, destination, overwrite, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Rename(string newName)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Rename already locks
            var newObject = DirectoryHandling.Rename(this, newName);
            CopyFrom(newObject); // CopyFrom has its own lock
            newObject.Dispose();
        }

        /// <inheritdoc/>
        public override async Task RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Rename already locks
            var newObject = await DirectoryHandling.RenameAsync(this, newName, cancellationToken);
            CopyFrom(newObject); // CopyFrom has its own lock
            newObject.Dispose();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            lock (StateLock)
            {
                // Check if already disposed
                try { ThrowIfDisposed(); }
                catch (ObjectDisposedException) { return; }

                // Disable watcher events before disposing
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= Watcher_Changed;
                    _watcher.Created -= Watcher_Changed;
                    _watcher.Deleted -= Watcher_Changed;
                }

                // Stop and dispose timer
                _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _debounceTimer?.Dispose();
                
                // Dispose watcher
                _watcher?.Dispose();
            }

            // Call base dispose (handles its own locking)
            base.Dispose();
            
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected override DirectoryInformation CreateInformationInstance()
        {
            return new DirectoryInformation(FullPath);
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
                var newObject = new DirectoryObject(FullPath);
                CopyFrom(newObject); // CopyFrom has its own lock
                newObject.Dispose();
                changedFiles.Clear();
            }
        }
    }
}
