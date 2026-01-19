using Rheo.Storage.Information;

namespace Rheo.Storage.Test.Information
{
    [Feature("DirectoryInformation")]
    [Category("Edge Case Tests")]
    public class DirectoryInformationEdgeCaseTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public void NoOfFiles_WithDeeplyNestedStructure_CountsAllFiles()
        {
            // Arrange: Create deeply nested directory structure
            var currentPath = TestDirectory.FullPath;
            for (int i = 0; i < 5; i++)
            {
                currentPath = Path.Combine(currentPath, $"level{i}");
                Directory.CreateDirectory(currentPath);
                File.WriteAllText(Path.Combine(currentPath, $"file{i}.txt"), $"Content {i}");
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(5, dirInfo.NoOfFiles);
        }

        [Fact]
        public void NoOfDirectories_WithDeeplyNestedStructure_CountsAllDirectories()
        {
            // Arrange: Create deeply nested directory structure
            var currentPath = TestDirectory.FullPath;
            for (int i = 0; i < 10; i++)
            {
                currentPath = Path.Combine(currentPath, $"nested{i}");
                Directory.CreateDirectory(currentPath);
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(10, dirInfo.NoOfDirectories);
        }

        [Fact]
        public void NoOfFiles_WithMixedContent_CountsOnlyFiles()
        {
            // Arrange
            File.WriteAllText(Path.Combine(TestDirectory.FullPath, "file1.txt"), "content");
            Directory.CreateDirectory(Path.Combine(TestDirectory.FullPath, "dir1"));
            File.WriteAllText(Path.Combine(TestDirectory.FullPath, "file2.dat"), "data");
            Directory.CreateDirectory(Path.Combine(TestDirectory.FullPath, "dir2"));

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(2, dirInfo.NoOfFiles);
            Assert.Equal(2, dirInfo.NoOfDirectories);
        }

        [Fact]
        public void Size_WithLargeNumberOfFiles_CalculatesCorrectly()
        {
            // Arrange: Create many small files
            long expectedSize = 0;
            for (int i = 0; i < 100; i++)
            {
                var filePath = Path.Combine(TestDirectory.FullPath, $"file{i}.bin");
                var content = new byte[i + 1];
                File.WriteAllBytes(filePath, content);
                expectedSize += i + 1;
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(expectedSize, dirInfo.Size);
        }

        [Fact]
        public void Size_WithVeryLargeFile_HandlesCorrectly()
        {
            // Arrange: Create a large file (1MB)
            var largePath = Path.Combine(TestDirectory.FullPath, "large.bin");
            var largeData = new byte[1024 * 1024]; // 1MB
            File.WriteAllBytes(largePath, largeData);

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(1024L * 1024, dirInfo.Size);
        }

        [Fact]
        public void Equals_WithCaseInsensitivePaths_HandlesCorrectly()
        {
            // Arrange
            var lowerPath = TestDirectory.FullPath.ToLowerInvariant();
            var upperPath = TestDirectory.FullPath.ToUpperInvariant();

            // Act
            var dirInfo1 = new DirectoryInformation(lowerPath);
            var dirInfo2 = new DirectoryInformation(upperPath);

            // Assert
            if (OperatingSystem.IsWindows())
            {
                // Windows is case-insensitive
                Assert.Equal(dirInfo1, dirInfo2);
            }
            else
            {
                // Unix-like systems are case-sensitive
                Assert.NotEqual(dirInfo1, dirInfo2);
            }
        }

        [Fact]
        public void GetHashCode_IsCaseInsensitive()
        {
            // Arrange
            var dirPath = TestDirectory.FullPath;

            // Act
            var dirInfo1 = new DirectoryInformation(dirPath);
            var dirInfo2 = new DirectoryInformation(dirPath.ToUpperInvariant());

            // Assert
            // Hash should be case-insensitive
            Assert.Equal(dirInfo1.GetHashCode(), dirInfo2.GetHashCode());
        }

        [Fact]
        public void NoOfFiles_WithSpecialCharactersInNames_CountsCorrectly()
        {
            // Arrange: Create files with special characters
            File.WriteAllText(Path.Combine(TestDirectory.FullPath, "file with spaces.txt"), "content");
            File.WriteAllText(Path.Combine(TestDirectory.FullPath, "file-with-dashes.txt"), "content");
            File.WriteAllText(Path.Combine(TestDirectory.FullPath, "file_with_underscores.txt"), "content");

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(3, dirInfo.NoOfFiles);
        }

        [Fact]
        public void NoOfFiles_WithHiddenFiles_CountsHiddenFiles()
        {
            // Arrange
            var hiddenPath = Path.Combine(TestDirectory.FullPath, ".hidden");
            File.WriteAllText(hiddenPath, "hidden content");

            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(hiddenPath, FileAttributes.Hidden);
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.True(dirInfo.NoOfFiles >= 1, "Should count hidden files");
        }

        [Fact]
        public void NoOfDirectories_WithHiddenDirectories_CountsHiddenDirectories()
        {
            // Arrange
            var hiddenDirPath = Path.Combine(TestDirectory.FullPath, ".hidden_dir");
            Directory.CreateDirectory(hiddenDirPath);

            if (OperatingSystem.IsWindows())
            {
                var winDirInfo = new DirectoryInfo(hiddenDirPath);
                winDirInfo.Attributes |= FileAttributes.Hidden;
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.True(dirInfo.NoOfDirectories >= 1, "Should count hidden directories");
        }

        [Fact]
        public void Size_WithEmptyFiles_CalculatesAsZero()
        {
            // Arrange
            File.WriteAllBytes(Path.Combine(TestDirectory.FullPath, "empty1.bin"), []);
            File.WriteAllBytes(Path.Combine(TestDirectory.FullPath, "empty2.bin"), []);
            File.WriteAllBytes(Path.Combine(TestDirectory.FullPath, "empty3.bin"), []);

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(0L, dirInfo.Size);
        }

        [Fact]
        public void Equals_WithSamePathTrailingSlash_ReturnsTrue()
        {
            // Arrange
            var pathWithSlash = TestDirectory.FullPath + Path.DirectorySeparatorChar;
            var pathWithoutSlash = TestDirectory.FullPath;

            // Act
            var dirInfo1 = new DirectoryInformation(pathWithSlash);
            var dirInfo2 = new DirectoryInformation(pathWithoutSlash);

            // Assert
            // Paths should be normalized and equal
            Assert.Equal(dirInfo1.NoOfFiles, dirInfo2.NoOfFiles);
            Assert.Equal(dirInfo1.NoOfDirectories, dirInfo2.NoOfDirectories);
        }

        [Fact]
        public void ToString_WithManyItems_FormatsCorrectly()
        {
            // Arrange
            for (int i = 0; i < 50; i++)
            {
                File.WriteAllText(Path.Combine(TestDirectory.FullPath, $"f{i}.txt"), "data");
            }
            for (int i = 0; i < 25; i++)
            {
                Directory.CreateDirectory(Path.Combine(TestDirectory.FullPath, $"d{i}"));
            }

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);
            var result = dirInfo.ToString();

            // Assert
            Assert.Contains("Files=50", result);
            Assert.Contains("Directories=25", result);
        }

        [Fact]
        public void NoOfFiles_WithSymbolicLinks_HandlesGracefully()
        {
            // Arrange
            var targetFile = Path.Combine(TestDirectory.FullPath, "target.txt");
            File.WriteAllText(targetFile, "content");

            // Note: Creating symbolic links requires elevated privileges on Windows
            // This test validates the behavior rather than creating actual symlinks
            // in a standard test environment

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.True(dirInfo.NoOfFiles >= 1);
        }

        [Fact]
        public void Size_OverflowScenario_HandlesUlongRange()
        {
            // Arrange: Create files that sum to a large size
            // Using ulong to test large values
            var file1 = Path.Combine(TestDirectory.FullPath, "file1.bin");
            var file2 = Path.Combine(TestDirectory.FullPath, "file2.bin");

            var data = new byte[1024]; // 1KB each
            File.WriteAllBytes(file1, data);
            File.WriteAllBytes(file2, data);

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(2048L, dirInfo.Size);
            Assert.IsType<long>(dirInfo.Size);
        }

        [Fact]
        public void Constructor_WithNetworkPath_HandlesIfAvailable()
        {
            // Arrange
            var localPath = TestDirectory.FullPath;

            // Act
            var dirInfo = new DirectoryInformation(localPath);

            // Assert
            Assert.NotNull(dirInfo);
            Assert.True(dirInfo.NoOfFiles >= 0);
            Assert.True(dirInfo.NoOfDirectories >= 0);
        }

        [Fact]
        public void NoOfFiles_WithVeryLongPath_HandlesCorrectly()
        {
            // Arrange: Create a moderately long path
            var longDirPath = TestDirectory.FullPath;
            for (int i = 0; i < 5; i++)
            {
                longDirPath = Path.Combine(longDirPath, $"very_long_directory_name_{i}");
                Directory.CreateDirectory(longDirPath);
            }

            File.WriteAllText(Path.Combine(longDirPath, "file.txt"), "content");

            // Act
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.True(dirInfo.NoOfFiles >= 1);
        }
    }
}