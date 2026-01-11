using Rheo.Storage.Information;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Information
{
    [Trait(TestTraits.Feature, "DirectoryInformation")]
    [Trait(TestTraits.Category, "Default Tests")]
    public class DirectoryInformationTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public void Constructor_WithValidPath_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(TestDir.Information);
        }

        [Fact]
        public void Constructor_WithNonExistentPath_ThrowsException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDir.FullPath, "nonexistent", "directory");

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => new DirectoryInformation(nonExistentPath));
        }

        [Fact]
        public async Task NoOfFiles_WithMultipleFiles_ReturnsCorrectCountAsync()
        {
            // Arrange
            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );
            _ = await TestDir.CreateTestFileAsync(
                ResourceType.Binary,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            // Ensure file system updates are recognized
            await Task.Delay(DirectoryObject.DefaultWatchInterval, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, TestDir.Information.NoOfFiles);
        }

        [Fact]
        public void NoOfFiles_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0, TestDir.Information.NoOfFiles);
        }

        [Fact]
        public async Task NoOfFiles_WithNestedFiles_CountsRecursivelyAsync()
        {
            // Arrange
            var subDirPath = Path.Combine(TestDir.FullPath, "subdir");
            Directory.CreateDirectory(subDirPath);

            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Create a file in subdirectory
            var subFile = Path.Combine(subDirPath, "test.txt");
            File.WriteAllText(subFile, "test content");

            // Act & Assert
            Assert.True(TestDir.Information.NoOfFiles >= 2, "Should count files in subdirectories");
        }

        [Fact]
        public void NoOfDirectories_WithMultipleSubdirectories_ReturnsCorrectCount()
        {
            // Arrange
            var testDirPath = TestDir.FullPath;
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir1"));
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir2"));
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir3"));

            // Act & Assert
            Assert.Equal(3, TestDir.Information.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0, TestDir.Information.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithNestedDirectories_CountsRecursively()
        {
            // Arrange
            var tempDirPath = TestDir.FullPath;
            var dir1 = Path.Combine(tempDirPath, "level1");
            var dir2 = Path.Combine(dir1, "level2");
            var dir3 = Path.Combine(dir2, "level3");
            Directory.CreateDirectory(dir3);

            // Act & Assert
            Assert.Equal(3, TestDir.Information.NoOfDirectories);
        }

        [Fact]
        public async Task Size_WithMultipleFiles_ReturnsTotalSizeAsync()
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

            var expectedSize = new FileInfo(testFile1.FullPath).Length + 
                               new FileInfo(testFile2.FullPath).Length;

            // Act & Assert
            Assert.Equal(expectedSize, TestDir.Information.Size);
        }

        [Fact]
        public void Size_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0L, TestDir.Information.Size);
        }

        [Fact]
        public async Task Size_WithNestedFiles_CalculatesRecursivelyAsync()
        {
            // Arrange
            var subDirPath = Path.Combine(TestDir.FullPath, "subdir_size");
            Directory.CreateDirectory(subDirPath);

            var rootFile = await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            var subFile = Path.Combine(subDirPath, "nested.bin");
            File.WriteAllBytes(subFile, [0x00, 0x01, 0x02, 0x03]);

            var expectedSize = new FileInfo(rootFile.FullPath).Length + 4;

            // Act & Assert
            Assert.Equal(expectedSize, TestDir.Information.Size);
        }

        [Fact]
        public void Equals_WithSameDirectory_ReturnsTrue()
        {
            // Arrange
            var dirInfo1 = TestDir.Information;

            // Act
            var dirInfo2 = new DirectoryInformation(TestDir.FullPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
            Assert.True(dirInfo1 == dirInfo2);
            Assert.False(dirInfo1 != dirInfo2);
        }

        [Fact]
        public void Equals_WithDifferentDirectories_ReturnsFalse()
        {
            // Arrange
            var dirInfo1 = TestDir.Information;
            var dir2Path = Path.Combine(TestDir.FullPath, "dir2");
            Directory.CreateDirectory(dir2Path);

            // Act
            var dirInfo2 = new DirectoryInformation(dir2Path);

            // Assert
            Assert.NotEqual(dirInfo1, dirInfo2);
            Assert.False(dirInfo1 == dirInfo2);
            Assert.True(dirInfo1 != dirInfo2);
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var dirInfo = TestDir.Information;

            // Act & Assert
            Assert.False(dirInfo.Equals(null));
            Assert.False(dirInfo == null);
            Assert.True(dirInfo != null);
        }

        [Fact]
        public async Task Equals_WithSamePathDifferentContent_ReturnsTrueAsync()
        {
            // Arrange
            var dirInfo1 = TestDir.Information;

            // Add a file to change the directory content
            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            var dirInfo2 = new DirectoryInformation(TestDir.FullPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
        }

        [Fact]
        public void GetHashCode_WithSameDirectory_ReturnsSameHash()
        {
            // Arrange
            var dirInfo1 = TestDir.Information;

            // Act
            var dirInfo2 = new DirectoryInformation(TestDir.FullPath);

            // Assert
            Assert.Equal(dirInfo1.GetHashCode(), dirInfo2.GetHashCode());
        }

        [Fact]
        public void ToString_WithValidDirectory_ReturnsFormattedString()
        {
            // Arrange
            var dirInfo = TestDir.Information;

            // Act
            var result = dirInfo.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Contains(dirInfo.DisplayName, result);
            Assert.Contains("Files=", result);
            Assert.Contains("Directories=", result);
            Assert.Contains("Size=", result);
        }

        [Fact]
        public async Task ToString_IncludesAllCountsAsync()
        {
            // Arrange - Use isolated subdirectory to avoid interference from other tests
            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            Directory.CreateDirectory(Path.Combine(TestDir.FullPath, "subdir"));

            // Act
            var dirInfo = TestDir.Information;
            var result = dirInfo.ToString();

            // Assert
            Assert.Contains("Files=1", result);
            Assert.Contains("Directories=1", result);
            Assert.Contains(dirInfo.FormattedSize, result);
        }

        [Fact]
        public void NoOfFiles_AccessedMultipleTimes_ReturnsConsistentResults()
        {
            // Arrange
            var dirInfo = TestDir.Information;

            // Act
            var count1 = dirInfo.NoOfFiles;
            var count2 = dirInfo.NoOfFiles;
            var count3 = dirInfo.NoOfFiles;

            // Assert
            Assert.Equal(count1, count2);
            Assert.Equal(count2, count3);
        }

        [Fact]
        public void NoOfDirectories_AccessedMultipleTimes_ReturnsConsistentResults()
        {
            // Arrange
            var dirInfo = TestDir.Information;

            // Act
            var count1 = dirInfo.NoOfDirectories;
            var count2 = dirInfo.NoOfDirectories;
            var count3 = dirInfo.NoOfDirectories;

            // Assert
            Assert.Equal(count1, count2);
            Assert.Equal(count2, count3);
        }

        [Fact]
        public async Task Size_IsCalculatedAtConstruction_NotDynamicallyAsync()
        {
            // Arrange
            var dirInfo = TestDir.Information;
            var initialSize = dirInfo.Size;

            // Act - Add a file after construction
            await TestDir.CreateTestFileAsync(
                ResourceType.Binary,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Assert - Size should remain the same
            Assert.Equal(initialSize, dirInfo.Size);
        }

        [Fact]
        public void Constructor_WithRelativePath_HandlesCorrectly()
        {
            // Arrange
            var relativePath = ".";

            // Act
            var dirInfo = new DirectoryInformation(relativePath);

            // Assert
            Assert.NotNull(dirInfo);
        }
    }
}