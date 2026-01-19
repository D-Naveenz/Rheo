using System.ComponentModel;
using System.Security;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides read-only information about a directory, including its size, file count, and subdirectory count. This
    /// class represents metadata for a specific directory on the file system.
    /// </summary>
    /// <remarks>DirectoryInformation is immutable and retrieves information recursively for the specified
    /// directory and all its subdirectories. If the application lacks sufficient permissions to access parts of the
    /// directory tree, some properties may return fallback values (such as -1 for counts or 0 for size). This class is
    /// thread-safe for concurrent read operations.</remarks>
    [ImmutableObject(true)]
    public sealed class DirectoryInformation : StorageInformation, IEquatable<DirectoryInformation>
    {
        private readonly DirectoryInfo _systemDirInfo;
        private readonly long _size;

        /// <summary>
        /// Initializes a new instance of the DirectoryInformation class for the specified absolute directory path.
        /// </summary>
        /// <param name="absolutePath">The absolute path of the directory to retrieve information for. The path must refer to an existing
        /// directory.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by absolutePath does not exist.</exception>
        public DirectoryInformation(string absolutePath) : base(absolutePath)
        {
            // Validate Path
            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException($"The specified directory does not exist: {absolutePath}");
            }

            _systemDirInfo = new(absolutePath);
            _size = CalculateDirectorySize(_systemDirInfo);
        }

        #region Properties: Counts
        /// <remarks>This property attempts to retrieve the file count recursively. If the application
        /// does not have the necessary permissions  to access the directory or its subdirectories, the property returns
        /// -1 to indicate that the operation could not be completed.</remarks>
        public int NoOfFiles
        {
            get
            {
                try
                {
                    // Attempt to get the number of files in the directory recursively
                    return _systemDirInfo.GetFiles("*", SearchOption.AllDirectories).Length;
                }
                catch (SecurityException)
                {
                    // Handle the case where access is denied
                    return -1; // or any other value indicating access is denied
                }
            }
        }

        /// <summary>
        /// Gets the total number of directories within the current directory and its subdirectories.
        /// </summary>
        /// <remarks>This property attempts to retrieve the count of all directories recursively. If a
        /// security exception occurs  (e.g., insufficient permissions to access the directory), the property returns
        /// -1.</remarks>
        public int NoOfDirectories
        {
            get
            {
                try
                {
                    // Attempt to get the number of folder in the directory recursively
                    return _systemDirInfo.GetDirectories("*", SearchOption.AllDirectories).Length;
                }
                catch (SecurityException)
                {
                    return -1;
                }
            }
        }

        #endregion

        #region Properties: Size
        /// <inheritdoc/>
        public override long Size => _size;

        #endregion

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{DisplayName} (Files={NoOfFiles}, Directories={NoOfDirectories}, Size={FormattedSize})";
        }

        /// <inheritdoc/>
        public bool Equals(DirectoryInformation? other)
        {
            if (other is null)
                return false;
            return string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.OrdinalIgnoreCase) &&
                   NoOfFiles == other.NoOfFiles &&
                   NoOfDirectories == other.NoOfDirectories;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is DirectoryInformation other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(AbsolutePath.ToLowerInvariant(), NoOfFiles, NoOfDirectories);
        }

        /// <inheritdoc/>
        public static bool operator ==(DirectoryInformation? left, DirectoryInformation? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(DirectoryInformation? left, DirectoryInformation? right)
        {
            return !(left == right);
        }

        private static long CalculateDirectorySize(DirectoryInfo systemDirInfo)
        {
            try
            {
                long size = 0;
                // Add file sizes.
                FileInfo[] files = systemDirInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    size += file.Length;
                }
                // Add subdirectory sizes.
                DirectoryInfo[] dirs = systemDirInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    size += CalculateDirectorySize(dir);
                }
                return size;
            }
            catch (SecurityException)
            {
                // If access is denied, return 0 as a fallback.
                return 0;
            }
        }
    }
}
