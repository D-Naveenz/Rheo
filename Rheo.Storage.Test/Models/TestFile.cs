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
        public bool IsTemporary => Information?.IsTemporary ?? false;

        /// <summary>
        /// Asynchronously writes the specified byte array to storage.
        /// </summary>
        /// <param name="content">The byte array containing the data to write. Cannot be null.</param>
        /// <param name="overwrite">true to overwrite existing content if present; otherwise, false to prevent overwriting.</param>
        /// <param name="progress">An optional progress reporter that receives storage operation progress updates. May be null if progress
        /// reporting is not required.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAsync(byte[] content, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var memoryStream = new MemoryStream(content);
            await WriteAsync(memoryStream, overwrite, cancellationToken);
        }

        public void Write(byte[] content, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var memoryStream = new MemoryStream(content);
            Write(memoryStream, overwrite);
        }
    }
}
