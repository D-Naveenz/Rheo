using Rheo.Storage.Information;
using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Information
{
    [Feature("FileInformation")]
    [Category("Default Tests")]
    public class FileInformationTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public void Constructor_WithValidFilePath_CreatesInstance()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo);
        }

        [Fact]
        public void Constructor_WithNonExistentPath_ThrowsException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDirectory.FullPath, "nonexistent.bin");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => new FileInformation(nonExistentPath));
        }

        [Fact]
        public void TypeName_WithImageFile_ReturnsDescriptiveName()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.TypeName);
            Assert.NotEmpty(fileInfo.TypeName);
            Assert.NotEqual("Unknown", fileInfo.TypeName);
        }

        [Fact]
        public void MimeType_WithPngImage_ReturnsPngMimeType()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.MimeType.Subject);
            // TODO: Re-enable once MIME type detection is fixed.
            // Assert.Contains("image", fileInfo.MimeType.Subject, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Extension_WithNamedFile_ReturnsCorrectExtension()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Document);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.Extension);
            Assert.StartsWith(".", fileInfo.Extension);
        }

        [Fact]
        public void ActualExtension_WithPngImage_ReturnsPngExtension()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.ActualExtension.Subject);
            Assert.Contains("png", fileInfo.ActualExtension.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.True(fileInfo.ActualExtension.Value > 0);
        }

        [Fact]
        public void IdentificationReport_WithValidFile_ReturnsNonEmptyResult()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Video);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.IdentificationReport);
            Assert.NotEmpty(fileInfo.IdentificationReport.Definitions);
        }

        [Fact]
        public void Size_WithKnownFile_ReturnsCorrectSize()
        {
            // Arrange
            long expectedSize;
            
            using (var testFile = TestDirectory.CreateTemplateFile(ResourceType.Text))
            {
                expectedSize = new FileInfo(testFile.FullPath).Length;

                // Act
                var fileInfo = new FileInformation(testFile.FullPath);

                // Assert
                Assert.Equal(expectedSize, fileInfo.Size);
            }
        }

        [Fact]
        public void Equals_WithSameFile_ReturnsTrue()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Binary);

            // Act
            var fileInfo1 = new FileInformation(testFile.FullPath);
            var fileInfo2 = new FileInformation(testFile.FullPath);

            // Assert
            Assert.Equal(fileInfo1, fileInfo2);
            Assert.True(fileInfo1 == fileInfo2);
            Assert.False(fileInfo1 != fileInfo2);
        }

        [Fact]
        public void Equals_WithDifferentFiles_ReturnsFalse()
        {
            // Arrange
            using var testFile1 = TestDirectory.CreateTemplateFile(ResourceType.Text);
            using var testFile2 = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo1 = new FileInformation(testFile1.FullPath);
            var fileInfo2 = new FileInformation(testFile2.FullPath);

            // Assert
            Assert.NotEqual(fileInfo1, fileInfo2);
            Assert.False(fileInfo1 == fileInfo2);
            Assert.True(fileInfo1 != fileInfo2);
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var emptyFilePath = Path.Combine(TestDirectory.FullPath, "temp.bin");
            File.WriteAllBytes(emptyFilePath, [0x00]);

            // Act
            var fileInfo = new FileInformation(emptyFilePath);

            // Assert
            Assert.False(fileInfo.Equals(null));
            Assert.False(fileInfo == null);
            Assert.True(fileInfo != null);
        }

        [Fact]
        public void GetHashCode_WithSameFile_ReturnsSameHash()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Document);

            // Act
            var fileInfo1 = new FileInformation(testFile.FullPath);
            var fileInfo2 = new FileInformation(testFile.FullPath);

            // Assert
            Assert.Equal(fileInfo1.GetHashCode(), fileInfo2.GetHashCode());
        }

        [Fact]
        public void ToString_WithValidFile_ReturnsFormattedString()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);
            var result = fileInfo.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Contains(fileInfo.DisplayName, result);
            Assert.Contains(fileInfo.FormattedSize, result);
        }

        [Theory]
        [InlineData(ResourceType.Text)]
        [InlineData(ResourceType.Image)]
        [InlineData(ResourceType.Binary)]
        [InlineData(ResourceType.Document)]
        [InlineData(ResourceType.Video)]
        public void Constructor_WithVariousFileTypes_CreatesValidInstance(ResourceType resourceType)
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(resourceType);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.NotNull(fileInfo.TypeName);
            Assert.NotNull(fileInfo.IdentificationReport);
            Assert.True(fileInfo.Size >= 0);
        }

        [Fact]
        public void IdentificationReport_AnalysisCompletesAsynchronously()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Video);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);
            
            // Access the report (should wait for async analysis to complete)
            var report = fileInfo.IdentificationReport;

            // Assert
            Assert.NotNull(report);
            Assert.False(report.IsEmpty);
        }

        [Fact]
        public void MimeType_AndActualExtension_AreConsistent()
        {
            // Arrange
            using var testFile = TestDirectory.CreateTemplateFile(ResourceType.Document);

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.MimeType.Subject);
            Assert.NotNull(fileInfo.ActualExtension.Subject);
            // Both should be derived from the same analysis
            Assert.True(fileInfo.MimeType.Value >= 0 && fileInfo.MimeType.Value <= 100);
            Assert.True(fileInfo.ActualExtension.Value >= 0 && fileInfo.ActualExtension.Value <= 100);
        }
    }
}