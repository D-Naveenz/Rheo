using System.Text;

namespace Rheo.Storage
{
    /// <summary>
    /// Represents an abstract base class for managing storage resources, such as files or directories.
    /// </summary>
    /// <remarks>The <see cref="StorageController"/> class provides a foundation for working with storage
    /// resources,  including properties for accessing metadata (e.g., name, size, creation date) and methods for
    /// performing  common operations such as copying, moving, renaming, and deleting resources.  Derived classes must
    /// implement abstract members to provide specific behavior for the storage type.</remarks>
    public abstract class StorageController
    {
        private const int MIN_BUFFER_SIZE = 1024; // 1KB
        private const int MAX_BUFFER_SIZE = 16 * 1024 * 1024; // 16MB

        public StorageController(string fileNameOrPath, AssertAs assert)
        {
            fileNameOrPath = VerifyPath(fileNameOrPath, assert);

            Name = Path.GetFileName(fileNameOrPath);

            ParentDirectory = GetDirectoryPath(fileNameOrPath);

            // Ensure the root path exists.
            if (!Directory.Exists(ParentDirectory))
            {
                Directory.CreateDirectory(ParentDirectory);
            }
        }

        #region Properties
        public string Name { get; protected set; }

        /// <summary>
        /// Gets the full path of the parent directory for the current file or directory.
        /// </summary>
        public string ParentDirectory { get; }

        /// <summary>
        /// Gets the full path of the file or directory by combining the parent directory and the name.
        /// </summary>
        public string FullPath => Path.Combine(ParentDirectory, Name);

        /// <summary>
        /// Gets the date and time when the entity was created.
        /// </summary>
        public abstract DateTime CreatedAt { get; }

        /// <summary>
        /// Gets a value indicating whether the resource is currently available.
        /// </summary>
        public abstract bool IsAvailable { get; }

        public long SizeInBytes => GetSize(UOM.Bytes);

        /// <summary>
        /// Gets the size of the buffer, in bytes, used for processing data.
        /// </summary>
        /// <remarks>The buffer size is dynamically calculated based on the total size in bytes, aiming to
        /// divide the data into approximately 100 chunks. If the calculated size is outside the allowed range, the
        /// buffer size defaults to the minimum or maximum value as appropriate.</remarks>
        public int BufferSize
        {
            get
            {
                long size = SizeInBytes;
                if (size <= 0)
                    return MIN_BUFFER_SIZE;
                else if (size < MIN_BUFFER_SIZE)
                    return MIN_BUFFER_SIZE;
                else if (size > MAX_BUFFER_SIZE)
                    return MAX_BUFFER_SIZE;
                else
                    return (int)Math.Min(MAX_BUFFER_SIZE, Math.Max(MIN_BUFFER_SIZE, size / 100)); // Aim for ~100 chunks
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the size of the storage in given unit. (default is Kilobytes)
        /// </summary>
        /// <param name="uom">The unit of measurement.</param>
        /// <returns>The size</returns>
        public abstract long GetSize(UOM uom = UOM.KB);

        /// <summary>
        /// Returns a string representation of the size, optionally formatted using the specified unit of measurement
        /// (UOM).
        /// </summary>
        /// <remarks>When the <paramref name="uom"/> parameter is provided, the size is formatted using
        /// the specified unit of measurement. If <paramref name="uom"/> is <see langword="null"/>, the method
        /// determines the most appropriate unit (bytes, kilobytes, megabytes, or gigabytes) based on the size in
        /// bytes.</remarks>
        /// <param name="uom">The unit of measurement to use for formatting the size. If <see langword="null"/>, the method automatically
        /// selects an appropriate unit based on the size in bytes.</param>
        /// <returns>A string representing the size, including the unit of measurement. If the size is unknown, the string
        /// "Unknown size" is returned.</returns>
        public string GetSizeString(UOM? uom = null)
        {
            if (uom.HasValue)
            {
                long size = GetSize(uom.Value);
                return size < 0 ? "Unknown size" : $"{size} {uom.Value}";
            }
            else
            {
                // Auto-select UOM
                long sizeInBytes = SizeInBytes;
                return sizeInBytes switch
                {
                    < 0 => "Unknown size",
                    < 1024 => $"{sizeInBytes} B",
                    < 1024 * 1024 => $"{sizeInBytes / 1024.0:F2} KB",
                    < 1024 * 1024 * 1024 => $"{sizeInBytes / (1024.0 * 1024):F2} MB",
                    _ => $"{sizeInBytes / (1024.0 * 1024 * 1024):F2} GB",
                };
            }
        }

        /// <summary>
        /// Asynchronously copies the current item to the specified destination.
        /// </summary>
        /// <remarks>If the destination already exists and <paramref name="overwrite"/> is <see
        /// langword="false"/>,  the operation will fail. The method supports cancellation via the <paramref
        /// name="cancellationToken"/>  parameter, and progress reporting if a <paramref name="progress"/> instance is
        /// provided.</remarks>
        /// <param name="destination">The path to the destination where the item will be copied.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination if it already exists.  <see langword="true"/> to
        /// overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="maxConcurrent">The maximum number of concurrent operations allowed during the copy process.  Must be greater than zero. The
        /// default is 4.</param>
        /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report progress updates during the copy operation. The
        /// progress value represents the number of bytes copied so far.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous copy operation.</returns>
        public abstract Task CopyAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously moves the current item to the specified destination.
        /// </summary>
        /// <remarks>This method performs the move operation asynchronously, allowing other tasks to run
        /// concurrently.  If <paramref name="overwrite"/> is <see langword="false"/> and the destination already
        /// exists,  the operation will fail. The <paramref name="maxConcurrent"/> parameter can be adjusted to optimize
        /// performance based on the environment and workload.</remarks>
        /// <param name="destination">The path to the destination where the item will be moved. This must be a valid and accessible path.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the destination if it already exists.  <see langword="true"/> to
        /// overwrite; otherwise, <see langword="false"/>.</param>
        /// <param name="maxConcurrent">The maximum number of concurrent operations allowed during the move. Must be a positive integer.  The
        /// default value is 4.</param>
        /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report progress updates during the move operation.  The
        /// progress value represents the percentage of the operation completed.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the operation to complete.  The operation
        /// will be canceled if the token is triggered.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous move operation.</returns>
        public abstract Task MoveAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the current resource asynchronously.
        /// </summary>
        /// <remarks>This method performs an asynchronous operation to delete the resource represented by
        /// the current instance.  Ensure that the resource is no longer needed before calling this method, as the
        /// deletion is irreversible.</remarks>
        /// <returns>A <see cref="Task"/> that represents the asynchronous delete operation.</returns>
        public abstract Task DeleteAsync();

        /// <summary>
        /// Renames the current entity to the specified new name asynchronously.
        /// </summary>
        /// <param name="newName">The new name to assign to the entity. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous rename operation.</returns>
        public abstract Task RenameAsync(string newName);

        public override bool Equals(object? obj)
        {
            if (obj is StorageController other)
            {
                return string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return FullPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies the validity of a specified file or directory path based on the provided assertion type.
        /// </summary>
        /// <remarks>
        /// This method ensures that the provided path adheres to the specified assertion type.
        /// It does not create or modify any files or directories. The method will throw an exception if the path is
        /// invalid or does not meet the specified assertion criteria.
        /// </remarks>
        /// <param name="path">The path to verify. Must not be null, empty, or contain invalid characters.</param>
        /// <param name="assert">
        /// Specifies whether the path should be validated as a file or a directory. Use <see cref="AssertAs.File"/> to
        /// validate a file path, or <see cref="AssertAs.Directory"/> to validate a directory path.
        /// </param>
        /// <returns>
        /// the full, absolute path.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if:
        /// <list type="bullet">
        ///   <item><description><paramref name="path"/> is null, empty, or contains only whitespace.</description></item>
        ///   <item><description><paramref name="path"/> contains invalid characters.</description></item>
        ///   <item><description>The path is asserted as a file but points to an existing directory.</description></item>
        ///   <item><description>The path is asserted as a directory but points to an existing file.</description></item>
        ///   <item><description>An invalid assertion type is provided in <paramref name="assert"/>.</description></item>
        /// </list>
        /// </exception>
        public static string VerifyPath(string path, AssertAs assert)
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

            if (assert == AssertAs.File)
            {
                // Check the path is point to an existing folder. If yes, the path should be invalid
                if (Directory.Exists(fullPath))
                {
                    throw new ArgumentException(
                        "The specified path points to an existing directory. Please provide a valid file path, not a directory.",
                        nameof(path)
                    );
                }
            }
            else if (assert == AssertAs.Directory)
            {
                // Check the path is point to an existing file. If yes, the path should be invalid
                if (File.Exists(fullPath))
                {
                    throw new ArgumentException(
                        "The specified path points to an existing file. Please provide a valid directory path, not a file.",
                        nameof(path)
                    );
                }
            }
            else
            {
                throw new ArgumentException("Please select a correct assertion method.", nameof(assert));
            }

            return fullPath;
        }

        public abstract override string ToString();

        /// <summary>
        /// Determines if two paths are on the same volume
        /// </summary>
        /// <param name="sourcepath"></param>
        /// <param name="destpath"></param>
        /// <returns></returns>
        protected static bool AreOnSameVolume(string sourcepath, string destpath)
        {
            try
            {
                var drive1 = Path.GetPathRoot(Path.GetFullPath(sourcepath));
                var drive2 = Path.GetPathRoot(Path.GetFullPath(destpath));

                return string.Equals(drive1, drive2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If we can't determine, assume different volumes
                return false;
            }
        }

        protected string Stringify(AssertAs modelType, string? displayName, string? displayType)
        {
            if (modelType != AssertAs.File && modelType != AssertAs.Directory)
            {
                throw new ArgumentException("modelType must be either File or Directory.", nameof(modelType));
            }

            StringBuilder sb = new();
            sb.Append($"[{modelType.ToString()}] {displayName ?? Name} ");
            sb.Append($"| Type: {displayType ?? "Unknown"} ");
            sb.Append($"| Size: {GetSizeString()} ");

            return sb.ToString();
        }

        /// <summary>
        /// Processes the specified destination path, ensuring it is a valid directory.
        /// </summary>
        /// <remarks>This method ensures that the provided destination path is a valid directory. If the
        /// directory does not exist, it will be created. The <paramref name="destination"/> parameter is
        /// updated to reflect the verified path.</remarks>
        /// <param name="destination">The destination path to process. This parameter is passed by reference and will be updated to a
        /// verified directory path.</param>
        /// <param name="overwrite">A value indicating whether existing content at the destination should be overwritten. The default
        /// value is <see langword="false"/>.</param>
        protected virtual void ProcessDestinationPath(ref string destination, bool overwrite = false)
        {
            // Verify the destination path. The destination provided should be a directory.
            destination = VerifyPath(destination, AssertAs.Directory);

            // Create the directory if it doesn't exist
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }
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
        #endregion
    }
}
