using System.Diagnostics;

namespace Rheo.Storage.Test.Models
{
    /// <summary>
    /// Represents a test file used for storing and managing test data within a test directory.
    /// </summary>
    /// <remarks>Use this class to interact with test files in automated test scenarios, including reading,
    /// writing, and managing file metadata. The test file is associated with a specific test directory and provides
    /// properties to identify its resource type and temporary status.</remarks>
    /// <param name="filePath">The full path to the file represented by this instance. Cannot be null or empty.</param>
    public class TestFile(string filePath) : FileObject(filePath)
    {
        /// <summary>
        /// Gets the type of resource represented by this instance.
        /// </summary>
        public ResourceType ResourceType { get; init; } = ResourceType.Unknown;

        /// <summary>
        /// Gets the test directory used for storing and accessing test files during execution.
        /// </summary>
        public TestDirectory TestDirectory { get; init; } = null!;

        /// <summary>
        /// Gets a value indicating whether the item is marked as temporary.
        /// </summary>
        public bool IsTemporary => Information.IsTemporary;

        /// <summary>
        /// Asynchronously writes the specified content to the file represented by this instance.
        /// </summary>
        /// <remarks>If overwrite is set to false and the file already exists, an exception is thrown.
        /// Progress updates are reported periodically if a progress reporter is provided. The method is asynchronous
        /// and does not block the calling thread.</remarks>
        /// <param name="content">The byte array containing the data to write to the file. Cannot be null.</param>
        /// <param name="overwrite">true to overwrite the file if it already exists; false to throw an exception if the file exists.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the number of bytes transferred and the transfer
        /// rate. May be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAsync(
            byte[] content,
            bool overwrite = false, 
            IProgress<StorageProgress>? progress = null,
            CancellationToken cancellationToken = default
            )
        {
            ArgumentNullException.ThrowIfNull(content);
            var bufferSize = GetBufferSize(Information.Size);

            using var memoryStream = new MemoryStream(content);
            using var destStream = new FileStream(
                FullPath,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                true);

            long totalBytes = memoryStream.Length;
            long totalBytesWritten = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            byte[] buffer = new byte[bufferSize];
            int bytesWritten;
            while ((bytesWritten = await memoryStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesWritten), cancellationToken);
                totalBytesWritten += bytesWritten;
                if (progress != null)
                {
                    double bytesPerSecond = totalBytesWritten / stopwatch.Elapsed.TotalSeconds;
                    progress.Report(new StorageProgress
                    {
                        TotalBytes = totalBytes,
                        BytesTransferred = totalBytesWritten,
                        BytesPerSecond = bytesPerSecond
                    });
                }
            }

            // Ensure all data is flushed to the file
            await destStream.FlushAsync(cancellationToken);

            // Raise the Event
            OnStorageChanged(new(FullPath, StorageChangeType.Created));
        }
    }
}
