using System.Diagnostics;

namespace Rheo.Storage.Test.Models
{
    /// <summary>
    /// Represents a test file that supports various resource types and provides functionality for creating, updating,
    /// and managing storage records.
    /// </summary>
    /// <remarks>A <see cref="TestFile"/> instance encapsulates a file created for testing purposes, with
    /// support for different resource types such as text, images, binary data, documents, and videos. It maintains a
    /// collection of storage records and provides metadata such as the timestamp of the last record.</remarks>
    public class TestFile(string filePath) : FileObject(filePath, false)
    {
        public ResourceType ResourceType { get; init; } = ResourceType.Unknown;
        public TestDirectory TestDirectory { get; init; } = null!;
        public new bool IsTemporary => TestDirectory.IsTemporary;

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

            using var memoryStream = new MemoryStream(content);
            using var destStream = new FileStream(
                FullPath,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                true);

            long totalBytes = memoryStream.Length;
            long totalBytesRead = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await memoryStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                if (progress != null)
                {
                    double bytesPerSecond = totalBytesRead / stopwatch.Elapsed.TotalSeconds;
                    progress.Report(new StorageProgress
                    {
                        TotalBytes = totalBytes,
                        BytesTransferred = totalBytesRead,
                        BytesPerSecond = bytesPerSecond
                    });
                }
            }
        }
    }
}
