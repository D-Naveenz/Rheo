using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Handling;

[Trait(TestTraits.Feature, "DirectoryObject")]
[Trait(TestTraits.Category, "Storage Operations")]
public class DirectoryObjectTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPath_CreatesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(TestDir.FullPath, "test_dir");

        // Act
        using var dirObj = new DirectoryObject(dirPath);

        // Assert
        Assert.True(Directory.Exists(dirPath));
        Assert.Equal(dirPath, dirObj.FullPath);
    }

    [Fact]
    public void Constructor_WithNonExistentDirectory_CreatesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(TestDir.FullPath, "new_directory");

        // Act
        using var dirObj = new DirectoryObject(dirPath);

        // Assert
        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public void Constructor_WithFilePath_ThrowsArgumentException()
    {
        // Arrange
        var filePath = Path.Combine(TestDir.FullPath, "test_file.txt");
        File.WriteAllText(filePath, "test");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DirectoryObject(filePath));
    }

    #endregion

    #region GetFiles Tests

    [Fact]
    public async Task GetFiles_ReturnsAllFiles()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        await TestDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var files = TestDir.GetFiles();

        // Assert
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task GetFiles_WithSearchPattern_ReturnsMatchingFiles()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(TestDir.FullPath, "test.log"), "log content");

        // Act
        var txtFiles = TestDir.GetFiles("*.txt");

        // Assert
        Assert.Single(txtFiles);
    }

    [Fact]
    public async Task GetFiles_WithAllDirectories_ReturnsFilesFromSubdirectories()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        var subDir = TestDir.CreateSubdirectory("sub");
        await subDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var allFiles = TestDir.GetFiles("*", SearchOption.AllDirectories);

        // Assert
        Assert.Equal(2, allFiles.Length);
    }

    #endregion

    #region GetFile Tests

    [Fact]
    public async Task GetFile_WithValidRelativePath_ReturnsFileObject()
    {
        // Arrange
        var testFile = await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var relativePath = Path.GetFileName(testFile.FullPath);

        // Act
        using var fileObj = TestDir.GetFile(relativePath);

        // Assert
        Assert.Equal(testFile.FullPath, fileObj.FullPath);
    }

    [Fact]
    public void GetFile_WithAbsolutePath_ThrowsArgumentException()
    {
        // Arrange
        var absolutePath = Path.Combine(TestDir.FullPath, "file.txt");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDir.GetFile(absolutePath));
    }

    [Fact]
    public void GetFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => TestDir.GetFile("nonexistent.txt"));
    }

    #endregion

    #region GetDirectories Tests

    [Fact]
    public void GetDirectories_ReturnsAllSubdirectories()
    {
        // Arrange
        TestDir.CreateSubdirectory("sub1");
        TestDir.CreateSubdirectory("sub2");

        // Act
        var dirs = TestDir.GetDirectories();

        // Assert
        Assert.Equal(2, dirs.Length);
    }

    [Fact]
    public void GetDirectories_WithSearchPattern_ReturnsMatchingDirectories()
    {
        // Arrange
        TestDir.CreateSubdirectory("test_dir");
        TestDir.CreateSubdirectory("other_dir");

        // Act
        var TestDirs = TestDir.GetDirectories("test_*");

        // Assert
        Assert.Single(TestDirs);
    }

    #endregion

    #region GetDirectory Tests

    [Fact]
    public void GetDirectory_WithValidRelativePath_ReturnsDirectoryObject()
    {
        // Arrange
        var subDir = TestDir.CreateSubdirectory("sub");
        var relativePath = Path.GetFileName(subDir.FullPath);

        // Act
        using var dirObj = TestDir.GetDirectory(relativePath);

        // Assert
        Assert.Equal(subDir.FullPath, dirObj.FullPath);
    }

    [Fact]
    public void GetDirectory_WithAbsolutePath_ThrowsArgumentException()
    {
        // Arrange
        var absolutePath = Path.Combine(TestDir.FullPath, "dir");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDir.GetDirectory(absolutePath));
    }

    [Fact]
    public void GetDirectory_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => TestDir.GetDirectory("nonexistent"));
    }

    #endregion

    #region Copy Tests

    [Fact]
    public async Task Copy_WithValidDestination_CopiesDirectory()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("copy_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var subDir = sourceDir.CreateSubdirectory("sub");
        await subDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDir.CreateSubdirectory("copy_dest_parent");

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(copiedDir.FullPath));
        Assert.Equal(2, copiedDir.GetFiles("*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Copy_PreservesEmptyDirectories()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("copy_empty_source");
        sourceDir.CreateSubdirectory("empty_sub");
        await sourceDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDir.CreateSubdirectory("copy_empty_dest");

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, overwrite: false);

        // Assert
        Assert.Single(copiedDir.GetDirectories());
    }

    [Fact]
    public async Task Copy_WithProgress_ReportsProgress()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("copy_progress_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        await sourceDir.CreateTestFileAsync(ResourceType.Document, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDir.CreateSubdirectory("copy_progress_dest");
        var progressReports = new List<StorageProgress>();
        var progress = new Progress<StorageProgress>(p => progressReports.Add(p));

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, progress, overwrite: false);

        // Assert
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task CopyAsync_WithValidDestination_CopiesDirectory()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("copy_async_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        await sourceDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDir.CreateSubdirectory("copy_async_dest");

        // Act
        using var copiedDir = await sourceDir.CopyAsync(destParent.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Directory.Exists(copiedDir.FullPath));
        Assert.Equal(2, copiedDir.GetFiles().Length);
    }

    #endregion

    #region Move Tests

    [Fact]
    public async Task Move_WithValidDestination_MovesDirectory()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("move_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceDir.FullPath;

        var destParent = TestDir.CreateSubdirectory("move_dest_parent");

        // Act
        using var movedDir = sourceDir.Move(destParent.FullPath, overwrite: false);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(movedDir.FullPath));
    }

    [Fact]
    public async Task Move_SameVolume_PerformsQuickMove()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("move_same_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDir.CreateSubdirectory("move_same_dest");
        var progressReports = new List<StorageProgress>();
        var progressReported = new TaskCompletionSource<bool>();
        var progress = new Progress<StorageProgress>(p =>
        {
            progressReports.Add(p);
            progressReported.TrySetResult(true);
        });

        // Act
        using var movedDir = sourceDir.Move(destParent.FullPath, progress, overwrite: false);

        // Wait for progress callback to execute (with timeout)
        var completedInTime = await Task.WhenAny(
            progressReported.Task, Task.Delay(1000, TestContext.Current.CancellationToken)) == progressReported.Task;

        // Assert
        Assert.True(completedInTime, "Progress callback did not execute within timeout");
        Assert.True(Directory.Exists(movedDir.FullPath));
        Assert.Single(progressReports); // Same volume should report single update
    }

    [Fact]
    public async Task MoveAsync_WithValidDestination_MovesDirectory()
    {
        // Arrange
        var sourceDir = TestDir.CreateSubdirectory("move_async_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceDir.FullPath;

        var destParent = TestDir.CreateSubdirectory("move_async_dest");

        // Act
        using var movedDir = await sourceDir.MoveAsync(destParent.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(movedDir.FullPath));
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_RemovesDirectory()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var dirPath = TestDir.FullPath;

        // Act
        TestDir.Delete();

        // Assert
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public async Task DeleteAsync_RemovesDirectory()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var dirPath = TestDir.FullPath;

        // Act
        await TestDir.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(dirPath));
    }

    #endregion

    #region Rename Tests

    [Fact]
    public void Rename_WithValidName_RenamesDirectory()
    {
        // Arrange
        var originalPath = TestDir.FullPath;
        var newName = "renamed_dir";

        // Act
        TestDir.Rename(newName);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(Path.Combine(BaseTestDirectory.FullPath, newName)));
        Assert.Equal(newName, TestDir.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("invalid<>name")]
    [InlineData("invalid|name")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDir.Rename(invalidName));
    }

    [Fact]
    public async Task RenameAsync_WithValidName_RenamesDirectory()
    {
        // Arrange
        var originalPath = TestDir.FullPath;
        var newName = "renamed_async_dir";

        // Act
        await TestDir.RenameAsync(newName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(Path.Combine(BaseTestDirectory.FullPath, newName)));
        Assert.Equal(newName, TestDir.Name);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Name_ReturnsDirectoryName()
    {
        // Act
        var name = TestDir.Name;

        // Assert
        Assert.Equal(Path.GetFileName(TestDir.FullPath), name);
    }

    [Fact]
    public async Task Information_ReturnsDirectoryInformation()
    {
        // Arrange
        await TestDir.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var info = TestDir.Information;

        // Assert
        Assert.NotNull(info);
        Assert.True(info.Size > 0);
    }

    #endregion

    #region FileSystemWatcher Tests

    [Fact]
    public async Task FileSystemWatcher_DetectsFileChanges()
    {
        // Arrange
        var initialInfo = TestDir.Information;

        // Wait for watcher to stabilize
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        await TestDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Wait for debounce timer (2 seconds as per DirectoryObject implementation)
        await Task.Delay(2500, TestContext.Current.CancellationToken);

        // Assert
        var updatedInfo = TestDir.Information;
        Assert.NotEqual(initialInfo.Size, updatedInfo.Size);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_DisposesObject()
    {
        // Act
        TestDir.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => TestDir.Copy(TestDir.FullPath, overwrite: false));
    }

    #endregion
}