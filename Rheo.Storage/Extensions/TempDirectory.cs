using Rheo.Storage.Contracts;
using Rheo.Storage.Information;
using System.ComponentModel;
using System.Diagnostics;

namespace Rheo.Storage.Extensions
{
    /// <summary>
    /// Represents a directory used for managing test files and related operations within the testing environment.
    /// </summary>
    /// <remarks>A TestDirectory provides functionality for creating, tracking, and interacting with
    /// directories that contain test files. Instances can be marked as temporary and are typically used to isolate test
    /// artifacts. The class offers methods for creating temporary directories and opening them in the system's file
    /// browser. Resources associated with the directory are cleaned up upon disposal. Thread safety is ensured during
    /// disposal operations.</remarks>
    public class TempDirectory : DirectoryObject
    {
        private readonly List<IStorageObject> _trackingObjects = [];

        private TempDirectory(string storagePath): base(storagePath)
        {
        }

        internal TempDirectory(DirectoryInformation information) : base(information)
        {
        }

        /// <summary>
        /// Creates a new temporary directory with a unique name and returns a wrapper for managing its lifetime.
        /// </summary>
        /// <remarks>The temporary directory is created with a unique name prefixed by "Rheo_" in the
        /// system's default temporary file location. The returned <see cref="TempDirectory"/> should be disposed when
        /// no longer needed to ensure the directory is deleted and resources are released.</remarks>
        /// <returns>A <see cref="TempDirectory"/> instance representing the newly created temporary directory.</returns>
        public static TempDirectory Create()
        {
            var tempDir = Directory.CreateTempSubdirectory("Rheo_");
            return new TempDirectory(tempDir.FullName);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Capture path BEFORE any disposal
            string pathToDelete;

            lock (StateLock)
            {
                // Early exit if already disposed
                if (IsDisposed) return;

                pathToDelete = FullPath;
            }

            // Dispose base (this acquires its own lock)
            base.Dispose();

            // Clean up test-specific resources (no lock needed - collections are owned by this instance)
            foreach (var file in _trackingObjects)
            {
                try { file.Dispose(); }
                catch { /* Best effort */ }
            }

            // Delete physical directory
            if (Directory.Exists(pathToDelete))
            {
                try
                {
                    Directory.Delete(pathToDelete, true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new file in the current storage location using the specified data and optional file name.
        /// </summary>
        /// <remarks>The created file is tracked by the storage object and will raise change notifications
        /// when modified. The file is created in the directory specified by the current storage object's
        /// path.</remarks>
        /// <param name="data">The byte array containing the data to write to the new file. Cannot be null.</param>
        /// <param name="name">The name to assign to the new file. If null or whitespace, a unique temporary name is generated.</param>
        /// <returns>An <see cref="IFileObject"/> representing the newly created file.</returns>
        public IFileObject CreateFile(byte[] data, string? name = null)
        {
            var fileName = string.IsNullOrWhiteSpace(name)? $"temp_{Guid.NewGuid():N}" : name;
            var filePath = Path.Combine(FullPath, fileName);

            var file = new FileObject(filePath);
            file.Write(data);
            _trackingObjects.Add(file);

            file.Changed += StorageObject_Changed;
            return file;
        }

        /// <summary>
        /// Asynchronously creates a new file containing the specified data and returns a reference to the created file
        /// object.
        /// </summary>
        /// <remarks>The returned file object is tracked for further operations. The file is created in
        /// the directory represented by this instance.</remarks>
        /// <param name="data">The byte array containing the data to write to the new file. Cannot be null.</param>
        /// <param name="name">The optional name for the new file. If null or whitespace, a unique temporary name is generated.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IFileObject"/>
        /// representing the newly created file.</returns>
        public async Task<IFileObject> CreateFileAsync(byte[] data, string? name = null, CancellationToken cancellationToken = default)
        {
            var fileName = string.IsNullOrWhiteSpace(name) ? $"temp_{Guid.NewGuid():N}" : name;
            var filePath = Path.Combine(FullPath, fileName);

            var file = new FileObject(filePath);
            using var stream = new MemoryStream(data);
            await file.WriteAsync(stream, cancellationToken);
            _trackingObjects.Add(file);

            file.Changed += StorageObject_Changed;
            return file;
        }

        /// <summary>
        /// Creates a new subdirectory within this directory and returns a corresponding TestDirectory instance.
        /// </summary>
        /// <remarks>The created subdirectory is added to the collection of tracked subdirectories for
        /// this TestDirectory instance. If a subdirectory with the specified name already exists, no exception is
        /// thrown and the existing directory is used.</remarks>
        /// <param name="name">The name of the subdirectory to create. If null or whitespace, a unique name is generated automatically.</param>
        /// <returns>A TestDirectory representing the newly created subdirectory.</returns>
        public TempDirectory CreateSubdirectory(string? name = null)
        {
            var dirName = string.IsNullOrWhiteSpace(name) ? $"Subdir_{Guid.NewGuid():N}" : name;
            var subdirPath = Path.Combine(FullPath, dirName);

            var subdir = new TempDirectory(subdirPath);
            _trackingObjects.Add(subdir);

            subdir.Changed += StorageObject_Changed;
            return subdir;
        }

        /// <summary>
        /// Opens the directory specified by the current object's full path in the system's default file browser.
        /// </summary>
        /// <remarks>This method supports Windows, macOS, and Linux platforms. On Linux, the method relies
        /// on the presence of the 'xdg-open' utility, which may not be available on all distributions. The method does
        /// not wait for the file browser process to exit.</remarks>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by the full path does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the file browser cannot be started, such as when the required system utility is unavailable.</exception>
        public void OpenInFileBrowser()
        {
            var path = FullPath;

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // explorer.exe expects the path as an argument
                    var psi = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return;
                }

                if (OperatingSystem.IsMacOS())
                {
                    // 'open' is the macOS command to open files/folders
                    var psi = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{path}\"",
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                    return;
                }

                if (OperatingSystem.IsLinux())
                {
                    // 'xdg-open' is the freedesktop standard opener; may be missing on minimal systems
                    var psi = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{path}\"",
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                    return;
                }

                throw new PlatformNotSupportedException("Unsupported operating system.");
            }
            catch (Win32Exception ex)
            {
                // Common when the helper isn't present or not found in PATH
                throw new InvalidOperationException("Failed to open the file browser. Ensure the OS has a default file manager available.", ex);
            }
        }

        private void StorageObject_Changed(object? sender, StorageChangedEventArgs e)
        {
            if (sender is IStorageObject storage && e.ChangeType == StorageChangeType.Deleted)
            {
                _trackingObjects.Remove(storage);

                // Remove the event listner
                storage.Changed -= StorageObject_Changed;
            }
        }
    }
}
