using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Handling;

[Feature("DirectoryObject")]
[Category("Storage Operations")]
public class DirectoryObjectTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPath_CreatesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(TestDirectory.FullPath, "test_dir");

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
        var dirPath = Path.Combine(TestDirectory.FullPath, "new_directory");

        // Act
        using var dirObj = new DirectoryObject(dirPath);

        // Assert
        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public void Constructor_WithFilePath_ThrowsArgumentException()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "test_file.txt");
        File.WriteAllText(filePath, "test");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DirectoryObject(filePath));
    }

    #endregion

    #region GetFiles Tests

    [Fact]
    public void GetFiles_ReturnsAllFiles()
    {
        // Arrange
        TestDirectory.CreateTemplateFile(ResourceType.Text); // ✅ Changed from CreateTemplateFileAsync
        TestDirectory.CreateTemplateFile(ResourceType.Binary); // ✅ Changed from CreateTemplateFileAsync

        // Act
        var files = TestDirectory.GetFiles();

        // Assert
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void GetFiles_WithSearchPattern_ReturnsMatchingFiles()
    {
        // Arrange
        TestDirectory.CreateTemplateFile(ResourceType.Text);
        File.WriteAllText(Path.Combine(TestDirectory.FullPath, "test.log"), "log content");

        // Act
        var txtFiles = TestDirectory.GetFiles("*.txt");

        // Assert
        Assert.Single(txtFiles);
    }

    [Fact]
    public async Task GetFiles_WithAllDirectories_ReturnsFilesFromSubdirectories()
    {
        // Arrange
        TestDirectory.CreateTemplateFile(ResourceType.Text);

        var subDir = TestDirectory.CreateSubdirectory("sub");
        subDir.CreateTemplateFile(ResourceType.Binary);

        // Act
        var allFiles = TestDirectory.GetFiles("*", SearchOption.AllDirectories);

        // Assert
        Assert.Equal(2, allFiles.Length);
    }

    #endregion

    #region GetFile Tests

    [Fact]
    public void GetFile_WithValidRelativePath_ReturnsFileObject()
    {
        // Arrange
        var testFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var relativePath = Path.GetFileName(testFile.FullPath);

        // Act
        using var fileObj = TestDirectory.GetFile(relativePath);

        // Assert
        Assert.Equal(testFile.FullPath, fileObj.FullPath);
    }

    [Fact]
    public void GetFile_WithAbsolutePath_ThrowsArgumentException()
    {
        // Arrange
        var absolutePath = Path.Combine(TestDirectory.FullPath, "file.txt");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDirectory.GetFile(absolutePath));
    }

    [Fact]
    public void GetFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => TestDirectory.GetFile("nonexistent.txt"));
    }

    #endregion

    #region GetDirectories Tests

    [Fact]
    public void GetDirectories_ReturnsAllSubdirectories()
    {
        // Arrange
        TestDirectory.CreateSubdirectory("sub1");
        TestDirectory.CreateSubdirectory("sub2");

        // Act
        var dirs = TestDirectory.GetDirectories();

        // Assert
        Assert.Equal(2, dirs.Length);
    }

    [Fact]
    public void GetDirectories_WithSearchPattern_ReturnsMatchingDirectories()
    {
        // Arrange
        TestDirectory.CreateSubdirectory("test_dir");
        TestDirectory.CreateSubdirectory("other_dir");

        // Act
        var TestDirs = TestDirectory.GetDirectories("test_*");

        // Assert
        Assert.Single(TestDirs);
    }

    #endregion

    #region GetDirectory Tests

    [Fact]
    public void GetDirectory_WithValidRelativePath_ReturnsDirectoryObject()
    {
        // Arrange
        var subDir = TestDirectory.CreateSubdirectory("sub");
        var relativePath = Path.GetFileName(subDir.FullPath);

        // Act
        using var dirObj = TestDirectory.GetDirectory(relativePath);

        // Assert
        Assert.Equal(subDir.FullPath, dirObj.FullPath);
    }

    [Fact]
    public void GetDirectory_WithAbsolutePath_ThrowsArgumentException()
    {
        // Arrange
        var absolutePath = Path.Combine(TestDirectory.FullPath, "dir");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDirectory.GetDirectory(absolutePath));
    }

    [Fact]
    public void GetDirectory_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => TestDirectory.GetDirectory("nonexistent"));
    }

    #endregion

    #region Copy Tests

    [Fact]
    public async Task Copy_WithValidDestination_CopiesDirectory()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("copy_source");
        sourceDir.CreateTemplateFile(ResourceType.Text);
        var subDir = sourceDir.CreateSubdirectory("sub");
        subDir.CreateTemplateFile(ResourceType.Binary);

        var destParent = TestDirectory.CreateSubdirectory("copy_dest_parent");

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(copiedDir.FullPath));
        Assert.Equal(2, copiedDir.GetFiles("*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void Copy_PreservesEmptyDirectories()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("copy_empty_source");
        sourceDir.CreateSubdirectory("empty_sub");
        sourceDir.CreateTemplateFile(ResourceType.Text);

        var destParent = TestDirectory.CreateSubdirectory("copy_empty_dest");

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, overwrite: false);

        // Assert
        Assert.Single(copiedDir.GetDirectories());
    }

    [Fact]
    public void Copy_WithProgress_ReportsProgress()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("copy_progress_source");
        sourceDir.CreateTemplateFile(ResourceType.Image);
        sourceDir.CreateTemplateFile(ResourceType.Document);

        var destParent = TestDirectory.CreateSubdirectory("copy_progress_dest");
        var progressReports = new List<StorageProgress>();
        var progress = new SyncProgress<StorageProgress>(p => progressReports.Add(p));

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, progress, overwrite: false);

        // Assert
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task CopyAsync_WithValidDestination_CopiesDirectory()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("copy_async_source");
        await sourceDir.CreateTemplateFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        await sourceDir.CreateTemplateFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDirectory.CreateSubdirectory("copy_async_dest");

        // Act
        using var copiedDir = await sourceDir.CopyAsync(destParent.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Directory.Exists(copiedDir.FullPath));
        Assert.Equal(2, copiedDir.GetFiles().Length);
    }

    #endregion

    #region Move Tests

    [Fact]
    public void Move_WithValidDestination_MovesDirectory()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("move_source");
        sourceDir.CreateTemplateFile(ResourceType.Text);
        var originalPath = sourceDir.FullPath;
        var destParent = TestDirectory.CreateSubdirectory("move_dest_parent");

        // Act
        sourceDir.Move(destParent.FullPath, overwrite: false);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(sourceDir.FullPath));
        Assert.Contains("move_dest_parent", sourceDir.FullPath);
    }

    [Fact]
    public async Task Move_SameVolume_PerformsQuickMove()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("move_same_source");
        await sourceDir.CreateTemplateFileAsync(ResourceType.Binary, TestContext.Current.CancellationToken);

        var destParent = TestDirectory.CreateSubdirectory("move_same_dest");
        var progressReports = new List<StorageProgress>();
        var progressReported = new TaskCompletionSource<bool>();
        var progress = new Progress<StorageProgress>(p =>
        {
            progressReports.Add(p);
            progressReported.TrySetResult(true);
        });

        // Act
        sourceDir.Move(destParent.FullPath, progress, overwrite: false);

        // Wait for progress callback to execute (with timeout)
        var completedInTime = await Task.WhenAny(
            progressReported.Task, Task.Delay(1000, TestContext.Current.CancellationToken)) == progressReported.Task;

        // Assert
        Assert.True(completedInTime, "Progress callback did not execute within timeout");
        Assert.True(Directory.Exists(sourceDir.FullPath));
        Assert.Single(progressReports); // Same volume should report single update
    }

    [Fact]
    public async Task MoveAsync_WithValidDestination_MovesDirectory()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("move_async_source");
        await sourceDir.CreateTemplateFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceDir.FullPath;

        var destParent = TestDirectory.CreateSubdirectory("move_async_dest");

        // Act
        await sourceDir.MoveAsync(destParent.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(sourceDir.FullPath));
        Assert.Contains("move_async_dest", sourceDir.FullPath);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_RemovesDirectory()
    {
        // Arrange
        TestDirectory.CreateTemplateFile(ResourceType.Text);
        var dirPath = TestDirectory.FullPath;

        // Act
        TestDirectory.Delete();

        // Assert
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public async Task DeleteAsync_RemovesDirectory()
    {
        // Arrange
        await TestDirectory.CreateTemplateFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var dirPath = TestDirectory.FullPath;

        // Act
        await TestDirectory.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(dirPath));
    }

    #endregion

    #region Rename Tests

    [Fact]
    public void Rename_WithValidName_RenamesDirectory()
    {
        // Arrange
        var originalPath = TestDirectory.FullPath;
        var originalName = TestDirectory.Name;
        var newName = "renamed_dir";

        // Act
        TestDirectory.Rename(newName);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(TestDirectory.FullPath));
        Assert.NotEqual(originalName, TestDirectory.Name);
        Assert.Equal(newName, TestDirectory.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("invalid<>name")]
    [InlineData("invalid|name")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TestDirectory.Rename(invalidName!));
    }

    [Fact]
    public async Task RenameAsync_WithValidName_RenamesDirectory()
    {
        // Arrange
        var originalPath = TestDirectory.FullPath;
        var originalName = TestDirectory.Name;
        var newName = "renamed_async_dir";

        // Act
        await TestDirectory.RenameAsync(newName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(originalPath));
        Assert.True(Directory.Exists(TestDirectory.FullPath));
        Assert.NotEqual(originalName, TestDirectory.Name);
        Assert.Equal(newName, TestDirectory.Name);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Name_ReturnsDirectoryName()
    {
        // Act
        var name = TestDirectory.Name;

        // Assert
        Assert.Equal(Path.GetFileName(TestDirectory.FullPath), name);
    }

    [Fact]
    public void Information_ReturnsDirectoryInformation()
    {
        // Arrange
        TestDirectory.CreateTemplateFile(ResourceType.Image);

        // Act
        var info = TestDirectory.Information;

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
        var initialInfo = TestDirectory.Information;

        // Wait for watcher to stabilize
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        await TestDirectory.CreateTemplateFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Wait for debounce timer (2 seconds as per DirectoryObject implementation)
        await Task.Delay(2500, TestContext.Current.CancellationToken);

        // Assert
        var updatedInfo = TestDirectory.Information;
        Assert.NotEqual(initialInfo.Size, updatedInfo.Size);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_DisposesObject()
    {
        // Act
        TestDirectory.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => TestDirectory.Copy(TestDirectory.FullPath, overwrite: false));
    }

    #endregion
}