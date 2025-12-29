using Moq;
using Rheo.Storage.Test.Storage.Models;

namespace Rheo.Storage.Test.Storage
{
    [Trait(TestTraits.Category, TestTraits.Storage)]
    [Trait(TestTraits.Feature, TestTraits.DirectoryController)]
    public class DirectoryControllerTests : IDisposable
    {
        private const string InvalidPattern = "\0";

        private readonly TestDirectory _testDir;
        private readonly string _testDirPath;
        private readonly string _subDirPath;
        private readonly List<string> _testFiles;
        private TestDirectory? _destDir;

        public DirectoryControllerTests()
        {
            _testFiles = [];

            // Create directory for testing
            _testDir = TestDirectory.Create();
            _testDirPath = _testDir.FullPath;
            _subDirPath = Path.Combine(_testDir.FullPath, "SubDir");
            Directory.CreateDirectory(_subDirPath);

            // Create test files to add some content
            _testFiles.Add(TestFile.Create(ResourceType.Video, _testDir).FullPath);
            _testFiles.Add(TestFile.Create(ResourceType.Text, _testDir).FullPath);
            _testFiles.Add(TestFile.Create(ResourceType.Binary, _subDirPath).FullPath);

            // Open the folder in file explorer for debugging
            // _testDir.OpenInFileBrowser();
        }

        [Fact]
        public void CreatedAt_ReturnsDirectoryCreationTime()
        {
            var controller = _testDir;
            var expected = Directory.GetCreationTime(_testDirPath);
            Assert.Equal(expected, controller.CreatedAt);
        }

        [Fact]
        public void IsAvailable_ReturnsTrueIfDirectoryExists()
        {
            var controller = _testDir;
            Assert.True(controller.IsAvailable);
        }

        [Fact]
        public void GetFiles_ReturnsFilesInDirectory()
        {
            var controller = _testDir;
            var files = controller.GetFiles();
            var topLevelFiles = files.Where(f => Path.GetDirectoryName(f) == _testDirPath);

            foreach (var testFile in topLevelFiles)
            {
                Assert.Contains(files, f => f == testFile);
            }
        }

        [Fact]
        public void GetFiles_ThrowsIOExceptionForInvalidPattern()
        {
            var controller = _testDir;
            Assert.Throws<IOException>(() => controller.GetFiles(InvalidPattern));
        }

        [Fact]
        public void GetDirectories_ReturnsSubDirectories()
        {
            var controller = _testDir;
            var dirs = controller.GetDirectories();
            Assert.Contains(dirs, d => d == _subDirPath);
        }

        [Fact]
        public void GetDirectories_ThrowsIOExceptionForInvalidPattern()
        {
            var controller = _testDir;
            Assert.Throws<IOException>(() => controller.GetDirectories(InvalidPattern));
        }

        [Fact]
        public void GetFile_ReturnsFileControllerForExistingFile()
        {
            var controller = _testDir;
            var fileController = controller.GetFile(Path.GetFileName(_testFiles[0]));
            Assert.True(fileController.IsAvailable);
        }

        [Fact]
        public void GetFile_ThrowsForAbsolutePath()
        {
            var controller = _testDir;
            Assert.Throws<ArgumentException>(() => controller.GetFile(Path.GetFullPath(_testFiles[0])));
        }

        [Fact]
        public void GetFile_ThrowsForNonExistentFile()
        {
            var controller = _testDir;
            Assert.Throws<FileNotFoundException>(() => controller.GetFile("nonexistent.txt"));
        }

        [Fact]
        public void GetDirectory_ReturnsDirectoryControllerForExistingSubDir()
        {
            var controller = _testDir;
            var dirController = controller.GetDirectory("SubDir");
            Assert.True(dirController.IsAvailable);
        }

        [Fact]
        public void GetDirectory_ThrowsForAbsolutePath()
        {
            var controller = _testDir;
            Assert.Throws<ArgumentException>(() => controller.GetDirectory(Path.GetFullPath(_subDirPath)));
        }

        [Fact]
        public void GetDirectory_ThrowsForNonExistentDirectory()
        {
            var controller = _testDir;
            Assert.Throws<DirectoryNotFoundException>(() => controller.GetDirectory("nonexistent"));
        }

        [Fact]
        public void GetSize_ReturnsCorrectSizeInKB()
        {
            var controller = _testDir;
            var DirectorySize = controller.GetSize(UOM.KB);
            var expectedSize = _testFiles.Sum(f => new FileInfo(f).Length);
            var expectedSizeInKB = (long)(expectedSize / Math.Pow(1024, 1));
            Assert.Equal(expectedSizeInKB, DirectorySize);
        }

        [Fact]
        public async Task CopyAsync_CopiesDirectoryToDestination_ReportsProgress()
        {
            // Arrange
            var controller = _testDir;
            _destDir = TestDirectory.Create();
            var destDirPath = _destDir.FullPath;

            var mockProgress = new Mock<IProgress<StorageProgress>>();
            StorageProgress? reportedProgress = null;
            mockProgress.Setup(p => p.Report(It.IsAny<StorageProgress>()))
                        .Callback<StorageProgress>(p => reportedProgress = p);

            // Act
            await controller.CopyAsync(destDirPath, overwrite: true, progress: mockProgress.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(_destDir.IsAvailable);
            Assert.True(File.Exists(_testFiles[0]));
            mockProgress.Verify(p => p.Report(It.IsAny<StorageProgress>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeleteAsync_DeletesDirectory()
        {
            var controller = _testDir;
            await controller.DeleteAsync();
            Assert.False(Directory.Exists(_testDirPath));
        }

        [Fact]
        public async Task MoveAsync_MovesDirectoryToDestination_ReportsProgress()
        {
            // Arrange
            var controller = _testDir;
            _destDir = TestDirectory.Create();
            var destDirPath = _destDir.FullPath;

            var mockProgress = new Mock<IProgress<StorageProgress>>();

            // Act
            await controller.MoveAsync(destDirPath, overwrite: true, progress: mockProgress.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(_destDir.IsAvailable);
            Assert.False(Directory.Exists(_testDirPath));
            mockProgress.Verify(p => p.Report(It.IsAny<StorageProgress>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RenameAsync_RenamesDirectory()
        {
            // Arrange
            var controller = _testDir;
            var newName = "RenamedDir";
            var newPath = Path.Combine(controller.ParentDirectory, newName);

            if (Directory.Exists(newPath))
                Directory.Delete(newPath, true);

            // Act
            await controller.RenameAsync(newName);

            // Assert
            Assert.True(Directory.Exists(newPath));
        }

        [Fact]
        public void ToString_ReturnsStringRepresentation()
        {
            var controller = _testDir;
            var result = controller.ToString();
            Assert.False(string.IsNullOrWhiteSpace(result));
        }

        public void Dispose()
        {
            // Cleanup test storages
            _destDir?.Dispose();
            _testDir?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}