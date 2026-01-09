using System.Security;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides information about a directory, including file and subdirectory counts and total size, for a specified
    /// absolute directory path.
    /// </summary>
    /// <remarks>Use this class to retrieve aggregate information about a directory and its contents, such as
    /// the number of files, number of subdirectories, and the total size of all files within the directory tree. If the
    /// application lacks sufficient permissions to access parts of the directory, some properties may return fallback
    /// values (such as -1 or 0) to indicate that the operation could not be completed.</remarks>
    public sealed class DirectoryInformation : StorageInformation, IEquatable<DirectoryInformation>
    {
        private readonly DirectoryInfo _systemDirInfo;

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
            Size = CalculateDirectorySize(_systemDirInfo);
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
        public override ulong Size { get; }

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
            return string.Equals(_absPath, other._absPath, StringComparison.OrdinalIgnoreCase) &&
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
            return HashCode.Combine(_absPath.ToLowerInvariant(), NoOfFiles, NoOfDirectories);
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

        private static ulong CalculateDirectorySize(DirectoryInfo systemDirInfo)
        {
            try
            {
                ulong size = 0;
                // Add file sizes.
                FileInfo[] files = systemDirInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    size += (ulong)file.Length;
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
