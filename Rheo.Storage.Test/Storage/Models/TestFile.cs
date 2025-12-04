using Rheo.Storage;
using Rheo.Test.Properties;
using System.Text;

namespace Rheo.Test.Storage.Models
{
    /// <summary>
    /// Represents a test file that supports various resource types and provides functionality for creating, updating,
    /// and managing storage records.
    /// </summary>
    /// <remarks>A <see cref="TestFile"/> instance encapsulates a file created for testing purposes, with
    /// support for different resource types such as text, images, binary data, documents, and videos. It maintains a
    /// collection of storage records and provides metadata such as the timestamp of the last record.</remarks>
    internal class TestFile : FileController, ITestStorage
    {
        private TestFile(string fileNameOrPath) : base(fileNameOrPath)
        {
        }

        public List<StorageRecord> StorageRecords { get; } = [];

        public DateTimeOffset LastRecordTime => StorageRecords.Count > 0 ? StorageRecords[^1].Timestamp : DateTimeOffset.MinValue;

        /// <summary>
        /// Creates a test file of the specified resource type in the given directory.
        /// </summary>
        /// <remarks>The content of the created file depends on the specified <paramref
        /// name="resourceType"/>: <list type="bullet"> <item><description><see cref="ResourceType.Text"/>: A sample
        /// text file.</description></item> <item><description><see cref="ResourceType.Image"/>: A small red dot PNG
        /// image.</description></item> <item><description><see cref="ResourceType.Binary"/>: A binary file with
        /// predefined content.</description></item> <item><description><see cref="ResourceType.Document"/>: A document
        /// file loaded from embedded resources.</description></item> <item><description><see
        /// cref="ResourceType.Video"/>: A video file loaded from embedded resources.</description></item> </list> The
        /// method writes the file to the specified directory and returns a <see cref="TestFile"/> object representing
        /// the created file.</remarks>
        /// <param name="resourceType">The type of resource to create. Determines the content and file extension of the test file.</param>
        /// <param name="directoryPath">The directory where the test file will be created. Must be a valid, writable path.</param>
        /// <returns>A <see cref="TestFile"/> instance representing the created test file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="resourceType"/> is not a valid <see cref="ResourceType"/> value.</exception>
        public static TestFile Create(ResourceType resourceType, string directoryPath)
        {
            byte[] resourceBytes;
            string fileExtension;

            switch (resourceType)
            {
                case ResourceType.Text:
                    resourceBytes = Encoding.UTF8.GetBytes("This is a sample text file for testing purposes.");
                    fileExtension = ".txt";
                    break;
                case ResourceType.Image:
                    // A small red dot PNG image in byte array form
                    resourceBytes = Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/wcAAwAB/6XK" +
                        "pAAAAABJRU5ErkJggg==");
                    fileExtension = ".png";
                    break;
                case ResourceType.Binary:
                    resourceBytes = [0x00, 0xFF, 0x7A, 0x3C, 0x5D, 0xA1];
                    fileExtension = ".bin";
                    break;
                case ResourceType.Document:
                    resourceBytes = Resources.TestData_Document;
                    fileExtension = Resources.TestData_Document_Extension;
                    break;
                case ResourceType.Video:
                    resourceBytes = Resources.TestData_Video;
                    fileExtension = Resources.TestData_Video_Extension;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null);
            }

            var fileName = $"Test{resourceType}{fileExtension}";
            var fullPath = Path.Combine(directoryPath, fileName);

            // Write the resource bytes to the file
            File.WriteAllBytes(fullPath, resourceBytes);

            // Create and return the TestFile instance
            return new TestFile(fullPath);
        }

        /// <summary>
        /// Creates a test file of the specified resource type in the given directory.
        /// </summary>
        /// <remarks>The content of the created file depends on the specified <paramref
        /// name="resourceType"/>: <list type="bullet"> <item><description><see cref="ResourceType.Text"/>: A sample
        /// text file.</description></item> <item><description><see cref="ResourceType.Image"/>: A small red dot PNG
        /// image.</description></item> <item><description><see cref="ResourceType.Binary"/>: A binary file with
        /// predefined content.</description></item> <item><description><see cref="ResourceType.Document"/>: A document
        /// file loaded from embedded resources.</description></item> <item><description><see
        /// cref="ResourceType.Video"/>: A video file loaded from embedded resources.</description></item> </list> The
        /// method writes the file to the specified directory and returns a <see cref="TestFile"/> object representing
        /// the created file.</remarks>
        /// <param name="resourceType">The type of resource to create. Determines the content and file extension of the test file.</param>
        /// <param name="directory">The directory where the file will be created. Must not be null.</param>
        /// <returns>A <see cref="TestFile"/> instance representing the created test file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="resourceType"/> is not a valid <see cref="ResourceType"/> value.</exception>
        public static TestFile Create(ResourceType resourceType, TestDirectory directory)
        {
            return Create(resourceType, directory.FullPath);
        }

        public void Update(OperationType operation)
        {
            StorageRecords.Add(new StorageRecord(this, operation));
        }
    }
}
