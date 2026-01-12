using Rheo.Storage.Test.Models;
using System.Text;

namespace Rheo.Storage.Test.Utilities
{
    /// <summary>
    /// Provides utility methods for creating and retrieving test files of various resource types for use in automated
    /// testing scenarios.
    /// </summary>
    /// <remarks>This static class offers methods to generate or locate sample files such as text, image,
    /// video, binary, and document files. If a requested file type does not exist in the test files directory, the
    /// class generates a minimal valid sample file of the appropriate type. These methods are intended to support test
    /// infrastructure by ensuring that valid test files are always available, regardless of the test
    /// environment.</remarks>
    internal static class TestFileProvider
    {
        private const string TestFilesDirectory = "TestFiles";

        /// <summary>
        /// Asynchronously creates a test file of the specified resource type within the given test directory.
        /// </summary>
        /// <param name="testDirectory">The test directory in which to create the file. Cannot be null.</param>
        /// <param name="resourceType">The type of resource file to create. Must be a defined value of the ResourceType enumeration.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a TestFile instance representing
        /// the created file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if resourceType is not a valid value of the ResourceType enumeration.</exception>
        public static async Task<TestFile> CreateTestFileAsync(
            this TestDirectory testDirectory, 
            ResourceType resourceType, 
            CancellationToken cancellationToken = default
            )
        {
            byte[] resourceBytes;
            string filepath;

            (resourceBytes, filepath) = resourceType switch
            {
                ResourceType.Text => GetTextFile(testDirectory.FullPath),
                ResourceType.Image => GetImageFile(testDirectory.FullPath),
                ResourceType.Video => GetVideoFile(testDirectory.FullPath),
                ResourceType.Binary => GetBinaryFile(testDirectory.FullPath),
                ResourceType.Document => GetDocumentFile(testDirectory.FullPath),
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null),
            };

            // Create and return the TestFile instance
            var file = new TestFile(filepath) 
            { 
                ResourceType = resourceType, 
                TestDirectory = testDirectory 
            };
            await file.WriteAsync(resourceBytes, true, cancellationToken);

            // Add the file to the test directory's collection
            testDirectory.TestFiles.Add(file);

            return file;
        }

        public static TestFile CreateTestFile(this TestDirectory testDirectory, ResourceType resourceType)
        {
            byte[] resourceBytes;
            string filepath;

            (resourceBytes, filepath) = resourceType switch
            {
                ResourceType.Text => GetTextFile(testDirectory.FullPath),
                ResourceType.Image => GetImageFile(testDirectory.FullPath),
                ResourceType.Video => GetVideoFile(testDirectory.FullPath),
                ResourceType.Binary => GetBinaryFile(testDirectory.FullPath),
                ResourceType.Document => GetDocumentFile(testDirectory.FullPath),
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null),
            };

            // Create and return the TestFile instance
            var file = new TestFile(filepath)
            {
                ResourceType = resourceType,
                TestDirectory = testDirectory
            };
            file.Write(resourceBytes, true);

            // Add the file to the test directory's collection
            testDirectory.TestFiles.Add(file);

            return file;
        }

        /// <summary>
        /// Retrieves the contents and target path of a text file for testing purposes.
        /// </summary>
        /// <param name="tempDir">An optional directory path in which to create the sample file if no text file is found. If null, a default
        /// temporary directory is used.</param>
        /// <returns>A tuple containing the byte contents of a text file and the full path where the file is or would be located.
        /// If no text file exists in the source directory, a sample file is generated and its contents and path are
        /// returned.</returns>
        public static (byte[], string) GetTextFile(string? tempDir = null)
        {
            var sourceDirectory = GetTestFilesPath();
            var targetDirectory = GetTargetTestFilesDirectory(tempDir);
            var sampleFilePath = CreateSampleFilePath(targetDirectory, ".txt");

            // Search for a .txt file in the test files directory
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    return (File.ReadAllBytes(file), sampleFilePath);
                }
            }

            // If no .txt file is found, create a sample text file in the target directory
            var sampleBytes = Encoding.UTF8.GetBytes("This is a sample text file for testing purposes.");
            return (sampleBytes, sampleFilePath);
        }

        /// <summary>
        /// Retrieves the contents and target file path of a PNG image file for testing purposes.
        /// </summary>
        /// <remarks>If no PNG file is found in the test files directory, a small sample PNG image is
        /// created and used instead. The returned file path indicates where the image would be located or created,
        /// which can be useful for test scenarios that require both the image data and its file path.</remarks>
        /// <param name="tempDir">An optional directory path where the sample image file will be created if no PNG file is found. If null, a
        /// default temporary directory is used.</param>
        /// <returns>A tuple containing a byte array with the image file contents and a string representing the target file path.
        /// If a PNG file exists in the test files directory, its contents are returned; otherwise, a sample PNG image
        /// is generated and returned.</returns>
        public static (byte[], string) GetImageFile(string? tempDir = null)
        {
            var sourceDirectory = GetTestFilesPath();
            var targetDirectory = GetTargetTestFilesDirectory(tempDir);
            var sampleFilePath = CreateSampleFilePath(targetDirectory, ".png");

            // Search for a .png file in the test files directory
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                if (Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    return (File.ReadAllBytes(file), sampleFilePath);
                }
            }

            // If no .png file is found, create a sample image file in the target directory
            // A small red dot PNG image in byte array form
            var sampleBytes = Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/wcAAwAB/6XK" +
                        "pAAAAABJRU5ErkJggg==");
            return (sampleBytes, sampleFilePath);
        }

        /// <summary>
        /// Retrieves the contents of a binary test file as a byte array, along with the path where a sample binary file
        /// would be created.
        /// </summary>
        /// <remarks>If a .bin file is present in the test files directory, its contents are returned.
        /// Otherwise, a sample binary file is created in the target directory and its contents are returned. The
        /// returned file path indicates where the sample file would be located, regardless of whether an existing or
        /// generated file is used.</remarks>
        /// <param name="tempDir">An optional directory path to use as the target location for the sample file. If null, a default temporary
        /// directory is used.</param>
        /// <returns>A tuple containing the binary file's contents as a byte array and the full path to the sample binary file.
        /// If no binary file exists in the source directory, a sample binary file is generated and its contents and
        /// path are returned.</returns>
        public static (byte[], string) GetBinaryFile(string? tempDir = null)
        {
            var sourceDirectory = GetTestFilesPath();
            var targetDirectory = GetTargetTestFilesDirectory(tempDir);
            var sampleFilePath = CreateSampleFilePath(targetDirectory, ".bin");

            // Search for a .bin file in the test files directory
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                if (Path.GetExtension(file).Equals(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    return (File.ReadAllBytes(file), sampleFilePath);
                }
            }

            // If no .bin file is found, create a sample binary file in the target directory
            byte[] sampleBytes = [0x00, 0xFF, 0x7A, 0x3C, 0x5D, 0xA1];
            return (sampleBytes, sampleFilePath);
        }

        /// <summary>
        /// Retrieves the contents of a sample PDF file as a byte array, along with the file path where the PDF is or
        /// would be located in the test environment.
        /// </summary>
        /// <remarks>If no PDF file is found in the test files directory, a minimal valid PDF is created
        /// in the target directory and returned. This method is intended for use in test scenarios where a sample PDF
        /// file is required.</remarks>
        /// <param name="tempDir">An optional directory path to use as the temporary target location for the sample PDF file. If null, a
        /// default test directory is used.</param>
        /// <returns>A tuple containing the PDF file's contents as a byte array and the full file path as a string. If a PDF file
        /// exists in the test files directory, its contents and path are returned; otherwise, a minimal valid PDF file
        /// is generated and its contents and path are returned.</returns>
        public static (byte[], string) GetDocumentFile(string? tempDir = null)
        {
            var sourceDirectory = GetTestFilesPath();
            var targetDirectory = GetTargetTestFilesDirectory(tempDir);
            var sampleFilePath = CreateSampleFilePath(targetDirectory, ".pdf");

            // Search for a .pdf file in the test files directory
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                if (Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return (File.ReadAllBytes(file), sampleFilePath);
                }
            }

            // If no .pdf file is found, create a minimal valid PDF file in the target directory
            // Minimal PDF structure (version 1.0)
            var pdfContent = """
            %PDF-1.0
            1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
            2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
            3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
            4 0 obj<</Length 44>>stream
            BT /F1 12 Tf 100 700 Td (Sample PDF) Tj ET
            endstream endobj
            5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
            xref
            0 6
            0000000000 65535 f 
            0000000009 00000 n 
            0000000056 00000 n 
            0000000115 00000 n 
            0000000259 00000 n 
            0000000352 00000 n 
            trailer<</Size 6/Root 1 0 R>>
            startxref
            421
            %%EOF
            """;
            var sampleBytes = Encoding.UTF8.GetBytes(pdfContent);
            return (sampleBytes, sampleFilePath);
        }

        /// <summary>
        /// Retrieves the contents of a sample MP4 video file and provides the path where it can be saved or used for
        /// testing purposes.
        /// </summary>
        /// <remarks>This method is intended for use in test scenarios where a valid MP4 file is required.
        /// If no MP4 file is found in the test files directory, a minimal MP4 file is created to ensure a valid file is
        /// always returned.</remarks>
        /// <param name="tempDir">An optional directory path to use as the target location for the sample file. If null, a default temporary
        /// directory is used.</param>
        /// <returns>A tuple containing a byte array with the MP4 file contents and a string representing the sample file path.
        /// If a sample MP4 file exists in the test files directory, its contents are returned; otherwise, a minimal
        /// valid MP4 file is generated.</returns>
        public static (byte[], string) GetVideoFile(string? tempDir = null)
        {
            var sourceDirectory = GetTestFilesPath();
            var targetDirectory = GetTargetTestFilesDirectory(tempDir);
            var sampleFilePath = CreateSampleFilePath(targetDirectory, ".mp4");

            // Search for a .mp4 file in the test files directory
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                if (Path.GetExtension(file).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return (File.ReadAllBytes(file), sampleFilePath);
                }
            }

            // If no .mp4 file is found, create a minimal valid MP4 file in the target directory
            // Minimal MP4 structure (1 frame, 1x1 pixel, H.264 encoded)
            var sampleBytes = Convert.FromBase64String(
                "AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAAIZnJlZQAAAe1tZGF0AAACrgYF//+q3EXpvebZ" +
                "gRQnWEAAAMABAAAAwDEQKAAACgAABQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAQA" +
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAfAAAAABAAAA" +
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAEeHRyYWsA" +
                "AABcdGtoZAAAAAMAAAAAAAAAAAAAAAEAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAEAAAABAAAAAAAAAAAAAAA" +
                "AAAEAAAAAAAAAAAAAAAAAAAEAAAABAAAAAQAAAAEAAAA");
            return (sampleBytes, sampleFilePath);
        }

        private static string CreateSampleFilePath(string testDirectory, string extention)
        {
            var fileName = $"sample_{Guid.NewGuid()}{extention}";
            var filePath = Path.Combine(testDirectory, fileName);
            // Ensure the file does not already exist
            if (File.Exists(filePath))
            {
                return CreateSampleFilePath(testDirectory, extention);
            }

            return filePath;
        }

        private static string GetTestFilesPath()
        {
            string testFilesPath = Path.GetFullPath(TestFilesDirectory);
            // Ensure the test files directory exists
            if (!Directory.Exists(testFilesPath))
            {
                Directory.CreateDirectory(testFilesPath);
            }
            return testFilesPath;
        }

        private static string GetTargetTestFilesDirectory(string? tempDir)
        {
            if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
            {
                return tempDir;
            }
            return GetTestFilesPath();
        }
    }
}
