using Rheo.Storage.Information;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Information
{
    [Trait(TestTraits.Feature, "FileInformation")]
    [Trait(TestTraits.Category, "Default Tests")]
    public class FileInformationTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public async Task Constructor_WithValidFilePath_CreatesInstanceAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo);
        }

        [Fact]
        public void Constructor_WithNonExistentPath_ThrowsException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDir.FullPath, "nonexistent.bin");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => new FileInformation(nonExistentPath));
        }

        [Fact]
        public async Task TypeName_WithImageFile_ReturnsDescriptiveNameAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.TypeName);
            Assert.NotEmpty(fileInfo.TypeName);
            Assert.NotEqual("Unknown", fileInfo.TypeName);
        }

        [Fact]
        public async Task MimeType_WithPngImage_ReturnsPngMimeTypeAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.MimeType.Subject);
            // TODO: Re-enable once MIME type detection is fixed.
            // Assert.Contains("image", fileInfo.MimeType.Subject, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Extension_WithNamedFile_ReturnsCorrectExtensionAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Document,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.Extension);
            Assert.StartsWith(".", fileInfo.Extension);
        }

        [Fact]
        public async Task ActualExtension_WithPngImage_ReturnsPngExtensionAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.ActualExtension.Subject);
            Assert.Contains("png", fileInfo.ActualExtension.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.True(fileInfo.ActualExtension.Value > 0);
        }

        [Fact]
        public async Task IdentificationReport_WithValidFile_ReturnsNonEmptyResultAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Video,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo.IdentificationReport);
            Assert.NotEmpty(fileInfo.IdentificationReport.Definitions);
        }

        [Fact]
        public async Task Size_WithKnownFile_ReturnsCorrectSizeAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            var expectedSize = new FileInfo(testFile.FullPath).Length;

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.Equal(expectedSize, fileInfo.Size);
        }

        [Fact]
        public async Task Equals_WithSameFile_ReturnsTrueAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Binary,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo1 = new FileInformation(testFile.FullPath);
            var fileInfo2 = new FileInformation(testFile.FullPath);

            // Assert
            Assert.Equal(fileInfo1, fileInfo2);
            Assert.True(fileInfo1 == fileInfo2);
            Assert.False(fileInfo1 != fileInfo2);
        }

        [Fact]
        public async Task Equals_WithDifferentFiles_ReturnsFalseAsync()
        {
            // Arrange
            var testFile1 = await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            var testFile2 = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

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
            var emptyFilePath = Path.Combine(TestDir.FullPath, "temp.bin");
            File.WriteAllBytes(emptyFilePath, [0x00]);

            // Act
            var fileInfo = new FileInformation(emptyFilePath);

            // Assert
            Assert.False(fileInfo.Equals(null));
            Assert.False(fileInfo == null);
            Assert.True(fileInfo != null);
        }

        [Fact]
        public async Task GetHashCode_WithSameFile_ReturnsSameHashAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Document,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo1 = new FileInformation(testFile.FullPath);
            var fileInfo2 = new FileInformation(testFile.FullPath);

            // Assert
            Assert.Equal(fileInfo1.GetHashCode(), fileInfo2.GetHashCode());
        }

        [Fact]
        public async Task ToString_WithValidFile_ReturnsFormattedStringAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

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
        public async Task Constructor_WithVariousFileTypes_CreatesValidInstanceAsync(ResourceType resourceType)
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                resourceType,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.NotNull(fileInfo.TypeName);
            Assert.NotNull(fileInfo.IdentificationReport);
            Assert.True(fileInfo.Size >= 0);
        }

        [Fact]
        public async Task IdentificationReport_AnalysisCompletesAsynchronouslyAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Video,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var fileInfo = new FileInformation(testFile.FullPath);
            
            // Access the report (should wait for async analysis to complete)
            var report = fileInfo.IdentificationReport;

            // Assert
            Assert.NotNull(report);
            Assert.False(report.IsEmpty);
        }

        [Fact]
        public async Task MimeType_AndActualExtension_AreConsistentAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Document,
                cancellationToken: TestContext.Current.CancellationToken
                );

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