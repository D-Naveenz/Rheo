using System.ComponentModel;
using System.Diagnostics;

namespace Rheo.Storage.Test.Models
{
    /// <summary>
    /// Represents a directory used for managing test files and related operations within the testing environment.
    /// </summary>
    /// <remarks>A TestDirectory provides functionality for creating, tracking, and interacting with
    /// directories that contain test files. Instances can be marked as temporary and are typically used to isolate test
    /// artifacts. The class offers methods for creating temporary directories and opening them in the system's file
    /// browser. Resources associated with the directory are cleaned up upon disposal. Thread safety is ensured during
    /// disposal operations.</remarks>
    public class TestDirectory : DirectoryObject
    {
        private TestDirectory(string storagePath)
            : base(storagePath)
        {
        }

        /// <summary>
        /// Gets the collection of test files associated with this instance.
        /// </summary>
        public List<TestFile> TestFiles { get; } = [];

        /// <summary>
        /// Gets the collection of test directories associated with this instance.
        /// </summary>
        public List<TestDirectory> TestDirectories { get; } = [];

        /// <summary>
        /// Gets a value indicating whether the item is marked as temporary.
        /// </summary>
        public bool IsTemporary => Information.IsTemporary;

        /// <summary>
        /// Creates a temporary test directory with a unique name in the system's temporary folder.
        /// </summary>
        /// <remarks>The directory name is prefixed with "Rheo_" followed by a unique identifier.  The
        /// directory is created immediately upon calling this method.</remarks>
        /// <returns>A <see cref="TestDirectory"/> instance representing the created directory.</returns>
        public static TestDirectory Create()
        {
            var tempDir = Directory.CreateTempSubdirectory("Rheo_");
            return new TestDirectory(tempDir.FullName);
        }

        /// <summary>
        /// Creates a new subdirectory within this directory and returns a corresponding TestDirectory instance.
        /// </summary>
        /// <remarks>The created subdirectory is added to the collection of tracked subdirectories for
        /// this TestDirectory instance. If a subdirectory with the specified name already exists, no exception is
        /// thrown and the existing directory is used.</remarks>
        /// <param name="name">The name of the subdirectory to create. If null or whitespace, a unique name is generated automatically.</param>
        /// <returns>A TestDirectory representing the newly created subdirectory.</returns>
        public TestDirectory CreateSubdirectory(string? name = null)
        {
            var dirName = string.IsNullOrWhiteSpace(name) ? $"Subdir_{Guid.NewGuid():N}" : name;
            var subdirPath = Path.Combine(FullPath, dirName);

            var subdir = new TestDirectory(subdirPath);

            TestDirectories.Add(subdir);
            return subdir;
        }

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
            foreach (var file in TestFiles)
            {
                try { file.Dispose(); }
                catch { /* Best effort */ }
            }

            foreach (var dir in TestDirectories)
            {
                try { dir.Dispose(); }
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
    }
}
