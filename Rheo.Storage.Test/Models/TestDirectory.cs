using System.ComponentModel;
using System.Diagnostics;

namespace Rheo.Storage.Test.Models
{
    /// <summary>
    /// Represents a temporary test directory with a unique name, created in the system's temporary folder.
    /// </summary>
    /// <remarks>This class provides functionality for managing a temporary directory, including tracking
    /// operations  performed on the directory and ensuring proper cleanup when the instance is disposed. The directory 
    /// is created immediately upon instantiation, and its name is prefixed with "Rheo_" followed by a unique 
    /// identifier.  <para> The <see cref="TestDirectory"/> class is thread-safe for disposal operations and implements 
    /// <see cref="IDisposable"/> to ensure that the directory and its contents are cleaned up when no longer needed.
    /// </para></remarks>
    public class TestDirectory : DirectoryObject, IDisposable
    {
        private readonly Lock _disposeLock = new();

        public new bool IsTemporary { get; init; }
        public List<TestFile> TestFiles { get; } = [];

        private TestDirectory(string storagePath)
            : base(storagePath)
        {
            IsTemporary = true;
        }

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

        public void Dispose()
        {
            // Clean up the directory and its contents
            // This operation might be executed in a concurrent environment
            lock (_disposeLock)
            {
                if (Directory.Exists(FullPath))
                {
                    try
                    {
                        Directory.Delete(FullPath, true);
                    }
                    catch (IOException)
                    {
                        // Handle the case where the directory is in use or cannot be deleted
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Handle the case where there are permission issues
                    }
                }
            }
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
