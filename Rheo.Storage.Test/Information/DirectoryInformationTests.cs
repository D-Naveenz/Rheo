using Rheo.Storage.Information;
using Rheo.Storage.Test.FileDefinitions;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Information
{
    [Trait(TestTraits.Feature, "DirectoryInformation")]
    [Trait(TestTraits.Category, "Default Tests")]
    public class DirectoryInformationTests(TestDirectoryFixture fixture) : IClassFixture<TestDirectoryFixture>
    {
        private readonly TestDirectoryFixture _fixture = fixture;

        private TestDirectory TestDir => _fixture.TestDir;

        [Fact]
        public void Constructor_WithValidPath_CreatesInstance()
        {
            // Arrange
            var dirPath = TestDir.FullPath;

            // Act
            var dirInfo = new DirectoryInformation(dirPath);

            // Assert
            Assert.NotNull(dirInfo);
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
            var subDir = TestDir.CreateSubdirectory();
            var testFile1 = await subDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            var testFile2 = await subDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );
            var testFile3 = await subDir.CreateTestFileAsync(
                ResourceType.Binary,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var dirInfo = new DirectoryInformation(subDir.FullPath);

            // Assert
            Assert.Equal(3, dirInfo.NoOfFiles);
        }

        [Fact]
        public void NoOfFiles_WithEmptyDirectory_ReturnsZero()
        {
            // Arrange
            var emptyDirPath = Path.Combine(TestDir.FullPath, "empty");
            Directory.CreateDirectory(emptyDirPath);

            // Act
            var dirInfo = new DirectoryInformation(emptyDirPath);

            // Assert
            Assert.Equal(0, dirInfo.NoOfFiles);
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

            // Act
            var dirInfo = new DirectoryInformation(TestDir.FullPath);

            // Assert
            Assert.True(dirInfo.NoOfFiles >= 2, "Should count files in subdirectories");
        }

        [Fact]
        public void NoOfDirectories_WithMultipleSubdirectories_ReturnsCorrectCount()
        {
            // Arrange
            var tempDir = Path.Combine(TestDir.FullPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path.Combine(tempDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(tempDir, "dir2"));
            Directory.CreateDirectory(Path.Combine(tempDir, "dir3"));

            // Act
            var dirInfo = new DirectoryInformation(tempDir);

            // Assert
            Assert.Equal(3, dirInfo.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithEmptyDirectory_ReturnsZero()
        {
            // Arrange
            var emptyDirPath = Path.Combine(TestDir.FullPath, "empty_for_dirs");
            Directory.CreateDirectory(emptyDirPath);

            // Act
            var dirInfo = new DirectoryInformation(emptyDirPath);

            // Assert
            Assert.Equal(0, dirInfo.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithNestedDirectories_CountsRecursively()
        {
            // Arrange
            var tempDir = Path.Combine(TestDir.FullPath, Guid.NewGuid().ToString());
            var dir1 = Path.Combine(tempDir, "level1");
            var dir2 = Path.Combine(dir1, "level2");
            var dir3 = Path.Combine(dir2, "level3");
            Directory.CreateDirectory(dir3);

            // Act
            var dirInfo = new DirectoryInformation(tempDir);

            // Assert
            Assert.Equal(3, dirInfo.NoOfDirectories);
        }

        [Fact]
        public async Task Size_WithMultipleFiles_ReturnsTotalSizeAsync()
        {
            // Arrange
            var subDir = TestDir.CreateSubdirectory();
            var testFile1 = await subDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            var testFile2 = await subDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            var expectedSize = (ulong)(new FileInfo(testFile1.FullPath).Length +
                                       new FileInfo(testFile2.FullPath).Length);

            // Act
            var dirInfo = new DirectoryInformation(subDir.FullPath);

            // Assert
            Assert.Equal(expectedSize, dirInfo.Size);
        }

        [Fact]
        public void Size_WithEmptyDirectory_ReturnsZero()
        {
            // Arrange
            var emptyDirPath = Path.Combine(TestDir.FullPath, "empty_for_size");
            Directory.CreateDirectory(emptyDirPath);

            // Act
            var dirInfo = new DirectoryInformation(emptyDirPath);

            // Assert
            Assert.Equal(0UL, dirInfo.Size);
        }

        [Fact]
        public async Task Size_WithNestedFiles_CalculatesRecursivelyAsync()
        {
            // Arrange
            var masterSubDir = TestDir.CreateSubdirectory();
            var subDirPath = Path.Combine(masterSubDir.FullPath, "subdir_size");
            Directory.CreateDirectory(subDirPath);

            var rootFile = await masterSubDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            var subFile = Path.Combine(subDirPath, "nested.bin");
            File.WriteAllBytes(subFile, [0x00, 0x01, 0x02, 0x03]);

            var expectedSize = (ulong)(new FileInfo(rootFile.FullPath).Length + 4);

            // Act
            var dirInfo = new DirectoryInformation(masterSubDir.FullPath);

            // Assert
            Assert.Equal(expectedSize, dirInfo.Size);
        }

        [Fact]
        public void Equals_WithSameDirectory_ReturnsTrue()
        {
            // Arrange
            var dirPath = TestDir.FullPath;

            // Act
            var dirInfo1 = new DirectoryInformation(dirPath);
            var dirInfo2 = new DirectoryInformation(dirPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
            Assert.True(dirInfo1 == dirInfo2);
            Assert.False(dirInfo1 != dirInfo2);
        }

        [Fact]
        public void Equals_WithDifferentDirectories_ReturnsFalse()
        {
            // Arrange
            var dir1Path = Path.Combine(TestDir.FullPath, "dir1");
            var dir2Path = Path.Combine(TestDir.FullPath, "dir2");
            Directory.CreateDirectory(dir1Path);
            Directory.CreateDirectory(dir2Path);

            // Act
            var dirInfo1 = new DirectoryInformation(dir1Path);
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
            var dirInfo = new DirectoryInformation(TestDir.FullPath);

            // Act & Assert
            Assert.False(dirInfo.Equals(null));
            Assert.False(dirInfo == null);
            Assert.True(dirInfo != null);
        }

        [Fact]
        public async Task Equals_WithSamePathDifferentContent_ReturnsTrueAsync()
        {
            // Arrange
            var dirPath = TestDir.FullPath;
            var dirInfo1 = new DirectoryInformation(dirPath);

            // Add a file to change the directory content
            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );

            var dirInfo2 = new DirectoryInformation(dirPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
        }

        [Fact]
        public void GetHashCode_WithSameDirectory_ReturnsSameHash()
        {
            // Arrange
            var dirPath = TestDir.FullPath;

            // Act
            var dirInfo1 = new DirectoryInformation(dirPath);
            var dirInfo2 = new DirectoryInformation(dirPath);

            // Assert
            Assert.Equal(dirInfo1.GetHashCode(), dirInfo2.GetHashCode());
        }

        [Fact]
        public void ToString_WithValidDirectory_ReturnsFormattedString()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(TestDir.FullPath, "sub"));

            // Act
            var dirInfo = new DirectoryInformation(TestDir.FullPath);
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
            // Arrange
            await TestDir.CreateTestFileAsync(
                ResourceType.Text,
                cancellationToken: TestContext.Current.CancellationToken
                );
            Directory.CreateDirectory(Path.Combine(TestDir.FullPath, "subdir"));

            // Act
            var dirInfo = new DirectoryInformation(TestDir.FullPath);
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
            var dirInfo = new DirectoryInformation(TestDir.FullPath);

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
            Directory.CreateDirectory(Path.Combine(TestDir.FullPath, "test_dir"));
            var dirInfo = new DirectoryInformation(TestDir.FullPath);

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
            var dirInfo = new DirectoryInformation(TestDir.FullPath);
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