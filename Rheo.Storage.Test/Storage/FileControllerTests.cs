using Moq;
using Rheo.Storage;
using Rheo.Test.Storage.Models;

namespace Rheo.Test.Storage
{
    [Trait(TestTraits.Category, TestTraits.Storage)]
    [Trait(TestTraits.Feature, TestTraits.FileController)]
    public class FileControllerTests : IDisposable
    {
        private readonly TestDirectory _testDir;
        private readonly TestFile _testFile;
        private readonly string _testFilePath;
        private TestDirectory? _destDir;

        public FileControllerTests()
        {
            // Create test storages
            _testDir = TestDirectory.Create();
            _testFile = TestFile.Create(ResourceType.Text, _testDir);
            _testFilePath = _testFile.FullPath;

            // Open the folder in file explorer for debugging
            // _testDir.OpenInFileBrowser();
        }

        [Fact]
        public void CreatedAt_ReturnsFileCreationTime()
        {
            var controller = _testFile;
            var expected = File.GetCreationTime(_testFile.FullPath);
            Assert.Equal(expected, controller.CreatedAt);
        }

        [Fact]
        public void Extension_ReturnsFileExtension()
        {
            var controller = _testFile;
            Assert.Equal(".txt", controller.Extension);
        }

        [Fact]
        public void IsAvailable_ReturnsTrueIfFileExists()
        {
            var controller = _testFile;
            Assert.True(controller.IsAvailable);
        }

        [Fact]
        public void GetSize_ReturnsCorrectSizeInKB()
        {
            var controller = _testFile;
            var expected = (long)(new FileInfo(_testFilePath).Length / Math.Pow(1024, 1));
            Assert.Equal(expected, controller.GetSize(UOM.KB));
        }

        [Fact]
        public async Task CopyAsync_CopiesFileToDestination()
        {
            var controller = _testFile;
            _destDir = TestDirectory.Create();
            var destDirPath = _destDir.FullPath;
            var destFilePath = Path.Combine(destDirPath, controller.Name);

            if (File.Exists(destFilePath))
                File.Delete(destFilePath);

            await controller.CopyAsync(destDirPath, overwrite: true);

            Assert.True(File.Exists(destFilePath));
        }

        [Fact]
        public async Task DeleteAsync_DeletesFile()
        {
            var controller = _testFile;
            await controller.DeleteAsync();
            Assert.False(File.Exists(_testFilePath));
        }

        [Fact]
        public async Task MoveAsync_MovesFileToDestination()
        {
            var controller = _testFile;
            _destDir = TestDirectory.Create();
            var destDirPath = _destDir.FullPath;
            var destFilePath = Path.Combine(destDirPath, controller.Name);

            if (File.Exists(destFilePath))
                File.Delete(destFilePath);

            await controller.MoveAsync(destDirPath, overwrite: true);

            Assert.True(File.Exists(destFilePath));
            Assert.False(File.Exists(_testFilePath));
        }

        [Fact]
        public async Task RenameAsync_RenamesFile()
        {
            var controller = _testFile;
            var newName = $"renamed_{controller.Name}";
            var newPath = Path.Combine(controller.ParentDirectory, newName);

            if (File.Exists(newPath))
                File.Delete(newPath);

            await controller.RenameAsync(newName);

            Assert.True(File.Exists(newPath));
        }

        [Fact]
        public void ToString_ReturnsStringRepresentation()
        {
            var controller = _testFile;
            var result = controller.ToString();
            Assert.False(string.IsNullOrWhiteSpace(result));
        }

        [Fact]
        public async Task CopyAsync_ReportsProgress()
        {
            // Arrange
            var controller = _testFile;
            _destDir = TestDirectory.Create();
            var destDirPath = _destDir.FullPath;
            var destFilePath = Path.Combine(destDirPath, controller.Name);

            var mockProgress = new Mock<IProgress<StorageProgress>>();
            StorageProgress? reportedProgress = null;
            mockProgress.Setup(p => p.Report(It.IsAny<StorageProgress>()))
                        .Callback<StorageProgress>(p => reportedProgress = p);

            // Act
            await controller.CopyAsync(destDirPath, overwrite: true, progress: mockProgress.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            mockProgress.Verify(p => p.Report(It.IsAny<StorageProgress>()), Times.AtLeastOnce());
            Assert.NotNull(reportedProgress);
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