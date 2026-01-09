using Rheo.Storage.Information;
using System.Diagnostics;

namespace Rheo.Storage
{
    /// <summary>
    /// Represents a file-based storage object that provides asynchronous operations for copying, moving, renaming, and
    /// deleting files, as well as access to file metadata.
    /// </summary>
    /// <remarks>FileObject enables manipulation of files on disk with support for progress reporting and
    /// cancellation in asynchronous operations. It ensures that file paths are validated and that operations such as
    /// copy and move handle cross-volume scenarios appropriately. Events are raised to notify about storage changes.
    /// This class is not thread-safe; concurrent access to the same instance should be avoided.</remarks>
    public class FileObject : StorageObject
    {
        /// <summary>
        /// Initializes a new instance of the FileObject class for the specified file path, creating the file if it does
        /// not already exist.
        /// </summary>
        /// <remarks>If the specified file does not exist, it is created. The constructor does not keep
        /// the file open after initialization.</remarks>
        /// <param name="path">The path to the file to be represented by this object. Can be either an absolute or relative path.</param>
        public FileObject(string path) : base(path)
        {
            path = FullPath; // Ensure base class has processed the path

            // Ensure the file exists without holding a stream open
            File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read).Dispose();

            // Load the information
            _informationInternal = CreateNewInformationInstance();
        }

        /// <summary>
        /// Gets metadata information about the storage object, such as size, attributes, and timestamps.
        /// </summary>
        public FileInformation Information => (FileInformation)_informationInternal!;

        /// <inheritdoc/>
        public override string Name => Path.GetFileName(FullPath);

        /// <inheritdoc/>
        public override async Task CopyAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ProcessDestinationPath(ref destination, overwrite);
            var bufferSize = GetBufferSize();

            using var sourceStream = new FileStream(
                FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                true);

            using var destStream = new FileStream(
                destination,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                true);

            long totalBytes = sourceStream.Length;
            long totalBytesRead = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Use a semaphore to throttle concurrent buffer reads/writes
            using var semaphore = new SemaphoreSlim(maxConcurrent > 0 ? maxConcurrent : 1);

            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;

                    if (progress != null)
                    {
                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        double bytesPerSecond = elapsedSeconds > 0 ? totalBytesRead / elapsedSeconds : 0;
                        progress.Report(new StorageProgress
                        {
                            TotalBytes = totalBytes,
                            BytesTransferred = totalBytesRead,
                            BytesPerSecond = bytesPerSecond
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                }

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            // Raise the Event
            OnStorageChanged(new(destination, StorageChangeType.Created));
        }

        /// <inheritdoc/>
        public override Task DeleteAsync()
        {
            var task = Task.Run(() =>
            {
                File.Delete(FullPath);
            });

            // Raise the Event
            OnStorageChanged(new(FullPath, StorageChangeType.Deleted));

            return task;
        }

        /// <inheritdoc/>
        public override async Task MoveAsync(
            string destination,
            bool overwrite = false,
            int maxConcurrent = 4,
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ProcessDestinationPath(ref destination, overwrite);

            if (AreOnSameVolume(FullPath, destination))
            {
                // Same volume move - fast operation (just directory entry update)
                if (overwrite && File.Exists(destination))
                {
                    File.Delete(destination);
                }

                // Use Task.Run for async context and cancellation support
                await Task.Run(() =>
                {
                    File.Move(FullPath, destination, overwrite);
                }, cancellationToken);

                progress?.Report(new StorageProgress
                {
                    TotalBytes = 1,
                    BytesTransferred = 1,
                    BytesPerSecond = 0
                });
            }
            else
            {
                // Cross-volume move - requires copy then delete
                await CopyAsync(destination, overwrite, maxConcurrent, progress, cancellationToken);
                await DeleteAsync();
            }

            // Raise the Event
            OnStorageChanged(new(destination, StorageChangeType.Relocated));
        }

        /// <inheritdoc/>
        public override async Task RenameAsync(string newName)
        {
            // Check if the name is null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("The name cannot be null, empty, or whitespace.", nameof(newName));
            }

            // Verify the name does not contain invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (newName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException("The name contains invalid characters.", nameof(newName));
            }

            string newPath = Path.Combine(Path.GetDirectoryName(FullPath) ?? throw new InvalidOperationException("Directory name is null"), newName);
            if (File.Exists(newPath))
            {
                throw new IOException($"A file with the name '{newName}' already exists.");
            }

            // Perform the rename (move)
            await Task.Run(() => File.Move(FullPath, newPath));

            // Raise the Event
            OnStorageChanged(new(newPath, StorageChangeType.Relocated));
        }

        /// <inheritdoc/>
        protected override FileInformation CreateNewInformationInstance()
        {
            return new FileInformation(FullPath);
        }

        /// <summary>
        /// Validates the specified file path and returns its fully qualified path. Throws an exception if the path
        /// refers to an existing directory.
        /// </summary>
        /// <param name="path">The file path to validate. This should refer to a file, not a directory.</param>
        /// <returns>A fully qualified file path if the specified path is valid and does not refer to an existing directory.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified path points to an existing directory.</exception>
        protected override string GetValidPath(string path)
        {
            var fullPath = base.GetValidPath(path);

            // Check the path is point to an existing folder. If yes, the path should be invalid
            if (Directory.Exists(fullPath))
            {
                throw new ArgumentException(
                    "The specified path points to an existing directory. Please provide a valid file path, not a directory.",
                    nameof(path)
                );
            }

            return fullPath;
        }


        /// <inheritdoc/>
        protected override void ProcessDestinationPath(ref string destination, bool overwrite = false)
        {
            base.ProcessDestinationPath(ref destination, overwrite);

            // Combine the destination directory with the current file name
            destination = Path.Combine(destination, Name);
            if (!overwrite && File.Exists(destination))
                throw new IOException("File already exists.");
        }
    }
}
