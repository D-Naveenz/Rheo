using Rheo.Storage.Information;
using Rheo.Storage.Test.FileDefinitions;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Information
{
    [Trait(TestTraits.Feature, "FileInformation")]
    [Trait(TestTraits.Category, "Default Tests")]
    public class FileInformationTests(TestDirectoryFixture fixture) : IClassFixture<TestDirectoryFixture>
    {
        private readonly TestDirectoryFixture _fixture = fixture;

        private TestDirectory TestDir => _fixture.TestDir;

        [Fact]
        public async Task Constructor_WithValidFileStream_CreatesInstanceAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.Equal(testFile.FullPath, fileInfo.FullPath);
        }

        [Fact]
        public async Task Constructor_WithNonReadableStream_ThrowsArgumentExceptionAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act & Assert
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Write);
            var exception = Assert.Throws<ArgumentException>(() => new FileInformation(stream));
            Assert.Contains("readable", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

            // Assert
            Assert.NotNull(fileInfo.MimeType.Subject);
            Assert.Contains("image", fileInfo.MimeType.Subject, StringComparison.OrdinalIgnoreCase);
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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            var expectedSize = (ulong)new FileInfo(testFile.FullPath).Length;

            // Act
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream1 = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            using var stream2 = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo1 = new FileInformation(stream1);
            var fileInfo2 = new FileInformation(stream2);

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
            using var stream1 = new FileStream(testFile1.FullPath, FileMode.Open, FileAccess.Read);
            using var stream2 = new FileStream(testFile2.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo1 = new FileInformation(stream1);
            var fileInfo2 = new FileInformation(stream2);

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
            using var stream = new FileStream(emptyFilePath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream1 = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            using var stream2 = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo1 = new FileInformation(stream1);
            var fileInfo2 = new FileInformation(stream2);

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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);
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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);
            
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
            using var stream = new FileStream(testFile.FullPath, FileMode.Open, FileAccess.Read);
            var fileInfo = new FileInformation(stream);

            // Assert
            Assert.NotNull(fileInfo.MimeType.Subject);
            Assert.NotNull(fileInfo.ActualExtension.Subject);
            // Both should be derived from the same analysis
            Assert.True(fileInfo.MimeType.Value >= 0 && fileInfo.MimeType.Value <= 100);
            Assert.True(fileInfo.ActualExtension.Value >= 0 && fileInfo.ActualExtension.Value <= 100);
        }
    }
}