using Rheo.Storage.Information;
using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Information
{
    [Feature("DirectoryInformation")]
    [Category("Default Tests")]
    public class DirectoryInformationTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public void Constructor_WithValidPath_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(TestDirectory.Information);
        }

        [Fact]
        public void Constructor_WithNonExistentPath_ThrowsException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDirectory.FullPath, "nonexistent", "directory");

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => new DirectoryInformation(nonExistentPath));
        }

        [Fact]
        public void NoOfFiles_WithMultipleFiles_ReturnsCorrectCount()
        {
            // Arrange
            using (TestDirectory.CreateTemplateFile(ResourceType.Text))
            using (TestDirectory.CreateTemplateFile(ResourceType.Image))
            using (TestDirectory.CreateTemplateFile(ResourceType.Binary))
            {
                // Files created
            } // All disposed here

            // Create fresh DirectoryInformation
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(3, dirInfo.NoOfFiles);
        }

        [Fact]
        public void NoOfFiles_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0, TestDirectory.Information.NoOfFiles);
        }

        [Fact]
        public void NoOfFiles_WithNestedFiles_CountsRecursively()
        {
            // Arrange
            var subDirPath = Path.Combine(TestDirectory.FullPath, "subdir");
            Directory.CreateDirectory(subDirPath);

            using (TestDirectory.CreateTemplateFile(ResourceType.Text))
            {
                // Create a file in subdirectory
                var subFile = Path.Combine(subDirPath, "test.txt");
                File.WriteAllText(subFile, "test content");
            } // Dispose FileObject

            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Act & Assert
            Assert.True(dirInfo.NoOfFiles >= 2, "Should count files in subdirectories");
        }

        [Fact]
        public void NoOfDirectories_WithMultipleSubdirectories_ReturnsCorrectCount()
        {
            // Arrange
            var testDirPath = TestDirectory.FullPath;
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir1"));
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir2"));
            Directory.CreateDirectory(Path.Combine(testDirPath, "dir3"));

            // Act & Assert
            Assert.Equal(3, TestDirectory.Information.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0, TestDirectory.Information.NoOfDirectories);
        }

        [Fact]
        public void NoOfDirectories_WithNestedDirectories_CountsRecursively()
        {
            // Arrange
            var tempDirPath = TestDirectory.FullPath;
            var dir1 = Path.Combine(tempDirPath, "level1");
            var dir2 = Path.Combine(dir1, "level2");
            var dir3 = Path.Combine(dir2, "level3");
            Directory.CreateDirectory(dir3);

            // Act & Assert
            Assert.Equal(3, TestDirectory.Information.NoOfDirectories);
        }

        [Fact]
        public void Size_WithMultipleFiles_ReturnsTotalSize()
        {
            // Arrange
            long expectedSize;

            using (var testFile1 = TestDirectory.CreateTemplateFile(ResourceType.Text))
            using (var testFile2 = TestDirectory.CreateTemplateFile(ResourceType.Image))
            {
                expectedSize = new FileInfo(testFile1.FullPath).Length +
                               new FileInfo(testFile2.FullPath).Length;
            } // Files disposed here

            // Create fresh DirectoryInformation after files are fully written and closed
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Act & Assert
            Assert.Equal(expectedSize, dirInfo.Size);
        }

        [Fact]
        public void Size_WithEmptyDirectory_ReturnsZero()
        {
            // Assert
            Assert.Equal(0L, TestDirectory.Information.Size);
        }

        [Fact]
        public void Size_WithNestedFiles_CalculatesRecursively()
        {
            // Arrange
            var subDirPath = Path.Combine(TestDirectory.FullPath, "subdir_size");
            Directory.CreateDirectory(subDirPath);

            long expectedSize;

            using (var rootFile = TestDirectory.CreateTemplateFile(ResourceType.Text))
            {
                var subFile = Path.Combine(subDirPath, "nested.bin");
                File.WriteAllBytes(subFile, [0x00, 0x01, 0x02, 0x03]);

                expectedSize = new FileInfo(rootFile.FullPath).Length + 4;
            } // Dispose FileObject to release handle

            // Create fresh DirectoryInformation after files are closed
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);

            // Act & Assert
            Assert.Equal(expectedSize, dirInfo.Size);
        }

        [Fact]
        public void Equals_WithSameDirectory_ReturnsTrue()
        {
            // Arrange
            var dirInfo1 = TestDirectory.Information;

            // Act
            var dirInfo2 = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
            Assert.True(dirInfo1 == dirInfo2);
            Assert.False(dirInfo1 != dirInfo2);
        }

        [Fact]
        public void Equals_WithDifferentDirectories_ReturnsFalse()
        {
            // Arrange
            var dirInfo1 = TestDirectory.Information;
            var dir2Path = Path.Combine(TestDirectory.FullPath, "dir2");
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
            var dirInfo = TestDirectory.Information;

            // Act & Assert
            Assert.False(dirInfo.Equals(null));
            Assert.False(dirInfo == null);
            Assert.True(dirInfo != null);
        }

        [Fact]
        public void Equals_WithSamePathDifferentContent_ReturnsTrue()
        {
            // Arrange
            var dirInfo1 = TestDirectory.Information;

            // Add a file to change the directory content
            using (TestDirectory.CreateTemplateFile(ResourceType.Text))
            {
                // File created
            }

            var dirInfo2 = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(dirInfo1, dirInfo2);
        }

        [Fact]
        public void GetHashCode_WithSameDirectory_ReturnsSameHash()
        {
            // Arrange
            var dirInfo1 = TestDirectory.Information;

            // Act
            var dirInfo2 = new DirectoryInformation(TestDirectory.FullPath);

            // Assert
            Assert.Equal(dirInfo1.GetHashCode(), dirInfo2.GetHashCode());
        }

        [Fact]
        public void ToString_WithValidDirectory_ReturnsFormattedString()
        {
            // Arrange
            var dirInfo = TestDirectory.Information;

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
        public void ToString_IncludesAllCounts()
        {
            // Arrange - Use isolated subdirectory to avoid interference from other tests
            using (TestDirectory.CreateTemplateFile(ResourceType.Text))
            {
                Directory.CreateDirectory(Path.Combine(TestDirectory.FullPath, "subdir"));
            } // Dispose FileObject

            // Create fresh DirectoryInformation
            var dirInfo = new DirectoryInformation(TestDirectory.FullPath);
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
            var dirInfo = TestDirectory.Information;

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
            var dirInfo = TestDirectory.Information;

            // Act
            var count1 = dirInfo.NoOfDirectories;
            var count2 = dirInfo.NoOfDirectories;
            var count3 = dirInfo.NoOfDirectories;

            // Assert
            Assert.Equal(count1, count2);
            Assert.Equal(count2, count3);
        }

        [Fact]
        public void Size_IsCalculatedAtConstruction_NotDynamicallyAsync()
        {
            // Arrange
            var dirInfo = TestDirectory.Information;
            var initialSize = dirInfo.Size;

            // Act - Add a file after construction
            using (TestDirectory.CreateTemplateFile(ResourceType.Binary))
            {
                // File created
            }

            // Assert - Size should remain the same (dirInfo was created before the file)
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