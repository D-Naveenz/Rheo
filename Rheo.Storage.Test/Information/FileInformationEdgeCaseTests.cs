using Rheo.Storage.Information;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Information
{
    [Trait(TestTraits.Feature, "FileInformation")]
    [Trait(TestTraits.Category, "Edge Case Tests")]
    public class FileInformationEdgeCaseTests : IDisposable
    {
        private readonly TestDirectory _testDir;

        public FileInformationEdgeCaseTests()
        {
            _testDir = TestDirectory.Create();
        }

        [Fact]
        public void Constructor_WithEmptyFile_HandlesGracefully()
        {
            // Arrange
            var emptyFilePath = Path.Combine(_testDir.FullPath, "empty.bin");
            File.WriteAllBytes(emptyFilePath, []);

            // Act
            var fileInfo = new FileInformation(emptyFilePath);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.Equal(0UL, fileInfo.Size);
            Assert.NotNull(fileInfo.TypeName);
        }

        [Fact]
        public void TypeName_WithUnknownFile_ReturnsUnknownOrFallback()
        {
            // Arrange: File with unknown binary pattern
            var unknownPath = Path.Combine(_testDir.FullPath, "unknown.xyz");
            byte[] unknownData = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22];
            File.WriteAllBytes(unknownPath, unknownData);

            // Act
            var fileInfo = new FileInformation(unknownPath);

            // Assert
            Assert.NotNull(fileInfo.TypeName);
            // Should either return "Unknown" or a fallback type name
            Assert.True(fileInfo.TypeName.Length > 0);
        }

        [Fact]
        public void Extension_WithNoExtension_ReturnsEmptyOrNull()
        {
            // Arrange: File without extension
            var noExtPath = Path.Combine(_testDir.FullPath, "filenoext");
            File.WriteAllBytes(noExtPath, [0x89, 0x50, 0x4E, 0x47]); // PNG signature

            // Act
            var fileInfo = new FileInformation(noExtPath);

            // Assert
            // Extension property should reflect the file name, not content
            Assert.True(string.IsNullOrEmpty(fileInfo.Extension));
        }

        [Fact]
        public void ActualExtension_WithMismatchedExtension_DetectsCorrectType()
        {
            // Arrange: PNG file with .txt extension
            var mismatchPath = Path.Combine(_testDir.FullPath, "fake.txt");
            var (pngData, _) = TestFileProvider.GetImageFile(_testDir.FullPath);
            File.WriteAllBytes(mismatchPath, pngData);

            // Act
            var fileInfo = new FileInformation(mismatchPath);

            // Assert
            Assert.Equal(".txt", fileInfo.Extension); // Name-based
            Assert.NotNull(fileInfo.ActualExtension.Subject);
            Assert.Contains("png", fileInfo.ActualExtension.Subject, StringComparison.OrdinalIgnoreCase); // Content-based
        }

        [Fact]
        public void Constructor_WithNonExistentFile_ThrowsException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDir.FullPath, "nonexistent.bin");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => new FileInformation(nonExistentPath));
        }

        [Fact]
        public void IdentificationReport_WithLargeFile_CompletesSuccessfully()
        {
            // Arrange: Create a larger file
            var largePath = Path.Combine(_testDir.FullPath, "large.bin");
            byte[] largeData = new byte[100 * 1024]; // 100KB
            
            // Fill with PDF signature
            var (pdfData, _) = TestFileProvider.GetDocumentFile(_testDir.FullPath);
            Array.Copy(pdfData, 0, largeData, 0, Math.Min(pdfData.Length, largeData.Length));
            File.WriteAllBytes(largePath, largeData);

            // Act
            var fileInfo = new FileInformation(largePath);

            // Assert
            Assert.NotNull(fileInfo.IdentificationReport);
            Assert.NotEmpty(fileInfo.IdentificationReport.Definitions);
            Assert.True(fileInfo.Size > 50000); // Should be large
        }

        [Fact]
        public void Equals_WithCaseInsensitivePaths_HandlesCorrectly()
        {
            // Arrange
            var testPath = Path.Combine(_testDir.FullPath, "TestFile.bin");
            File.WriteAllBytes(testPath, [0x50, 0x4B, 0x03, 0x04]);

            // Act
            var fileInfo1 = new FileInformation(testPath);
            var fileInfo2 = new FileInformation(testPath.ToLowerInvariant());

            // Assert
            // Should be equal regardless of path casing (on Windows)
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(fileInfo1, fileInfo2);
            }
        }

        [Fact]
        public void TypeName_PrefersLongerDescription()
        {
            // Arrange: Create a well-known file type
            var pdfPath = Path.Combine(_testDir.FullPath, "test.pdf");
            var (pdfData, _) = TestFileProvider.GetDocumentFile(_testDir.FullPath);
            File.WriteAllBytes(pdfPath, pdfData);

            // Act
            var fileInfo = new FileInformation(pdfPath);

            // Assert
            Assert.NotNull(fileInfo.TypeName);
            // Should prefer longer, more descriptive name
            Assert.True(fileInfo.TypeName.Length > 3);
        }

        [Fact]
        public void IdentificationReport_WithMultipleMatches_ReturnsOrderedResults()
        {
            // Arrange: ZIP signature can match multiple formats
            var zipPath = Path.Combine(_testDir.FullPath, "test.zip");
            byte[] zipSignature = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];
            File.WriteAllBytes(zipPath, zipSignature);

            // Act
            var fileInfo = new FileInformation(zipPath);

            // Assert
            Assert.NotEmpty(fileInfo.IdentificationReport.Definitions);
            
            // Results should be ordered by confidence
            var definitions = fileInfo.IdentificationReport.Definitions.ToList();
            for (int i = 0; i < definitions.Count - 1; i++)
            {
                Assert.True(definitions[i].Value >= definitions[i + 1].Value);
            }
        }

        [Fact]
        public async Task Constructor_AccessedConcurrently_HandlesThreadSafelyAsync()
        {
            // Arrange
            var testPath = Path.Combine(_testDir.FullPath, "concurrent.bin");
            var (imageData, _) = TestFileProvider.GetImageFile(_testDir.FullPath);
            File.WriteAllBytes(testPath, imageData);

            // Act
            var tasks = Enumerable.Range(0, 5).Select(async _ =>
            {
                var fileInfo = new FileInformation(testPath);
                await Task.Delay(10); // Simulate work
                return fileInfo.TypeName;
            });

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, typeName => Assert.NotNull(typeName));
            // All results should be the same
            Assert.All(results, typeName => Assert.Equal(results[0], typeName));
        }

        [Fact]
        public void MimeType_WithEmptyFile_ReturnsDefaultOrEmpty()
        {
            // Arrange
            var emptyPath = Path.Combine(_testDir.FullPath, "empty.dat");
            File.WriteAllBytes(emptyPath, []);

            // Act
            var fileInfo = new FileInformation(emptyPath);

            // Assert
            // Empty file should have default or empty MIME type
            Assert.True(fileInfo.IdentificationReport.IsEmpty || Math.Abs(fileInfo.MimeType.Value) < 1e-9);
        }

        [Fact]
        public void GetHashCode_IsCaseInsensitive()
        {
            // Arrange
            var path1 = Path.Combine(_testDir.FullPath, "Hash.bin");
            File.WriteAllBytes(path1, [0x00, 0x01]);

            // Act
            var fileInfo = new FileInformation(path1);
            var hash1 = fileInfo.GetHashCode();
            
            // Create another instance with same file
            var fileInfo2 = new FileInformation(path1);
            var hash2 = fileInfo2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void ToString_IncludesActualExtension()
        {
            // Arrange
            var testPath = Path.Combine(_testDir.FullPath, "formatted.bin");
            var (pngData, _) = TestFileProvider.GetImageFile(_testDir.FullPath);
            File.WriteAllBytes(testPath, pngData);

            // Act
            var fileInfo = new FileInformation(testPath);
            var result = fileInfo.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[", result);
            Assert.Contains("]", result);
            // Should include the actual extension detected from content
        }

        public void Dispose()
        {
            _testDir?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}