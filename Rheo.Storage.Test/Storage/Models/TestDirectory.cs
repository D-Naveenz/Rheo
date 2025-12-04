using Rheo.Storage;
using System.ComponentModel;
using System.Diagnostics;

namespace Rheo.Test.Storage.Models
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
    internal class TestDirectory : DirectoryController, ITestStorage, IDisposable
    {
        private readonly Lock _disposeLock = new();

        private TestDirectory(string storagePath)
            : base(storagePath)
        {
            // Initialize with a creation record
            StorageRecords.Add(new StorageRecord(this, OperationType.Create));
        }

        public List<StorageRecord> StorageRecords { get; } = [];

        public DateTimeOffset LastRecordTime => StorageRecords.Count > 0 ? StorageRecords[^1].Timestamp : DateTimeOffset.MinValue;

        /// <summary>
        /// Creates a temporary test directory with a unique name in the system's temporary folder.
        /// </summary>
        /// <remarks>The directory name is prefixed with "Rheo_" followed by a unique identifier.  The
        /// directory is created immediately upon calling this method.</remarks>
        /// <returns>A <see cref="TestDirectory"/> instance representing the created directory.</returns>
        public static TestDirectory Create()
        {
            var uniqueId = Guid.NewGuid().ToString();
            // Combine the prefix, unique ID, and temporary path to create the full path
            var folderName = $"Rheo_{uniqueId}";
            var fullPath = Path.Combine(Path.GetTempPath(), folderName);

            // Create the directory at the specified path
            Directory.CreateDirectory(fullPath);

            return new TestDirectory(fullPath);
        }

        public void Update(OperationType operation)
        {
            StorageRecords.Add(new StorageRecord(this, operation));
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

        public void OpenInFileBrowser()
        {
            var path = Path.GetFullPath(FullPath);

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
