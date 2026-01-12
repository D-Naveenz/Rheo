using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Handling;

[Trait(TestTraits.Feature, "FileObject")]
[Trait(TestTraits.Category, "Storage Operations")]
public class FileObjectTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPath_CreatesFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "test.txt");

        // Act
        using var fileObj = new FileObject(filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.Equal(filePath, fileObj.FullPath);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_CreatesFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "newfile.txt");

        // Act
        using var fileObj = new FileObject(filePath);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Constructor_WithRelativePath_CreatesFileInCurrentDirectory()
    {
        // Arrange
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(TestDirectory.FullPath);

        try
        {
            // Act
            using var fileObj = new FileObject("relative.txt");

            // Assert
            Assert.True(File.Exists(Path.Combine(TestDirectory.FullPath, "relative.txt")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    #endregion

    #region Copy Tests

    [Fact]
    public async Task Copy_WithValidDestination_CopiesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        // Act
        using var copiedFile = sourceFile.Copy(destDir.FullPath, overwrite: false);

        // Assert
        Assert.True(File.Exists(copiedFile.FullPath));
        Assert.Equal(sourceFile.Information.Size, copiedFile.Information.Size);
        Assert.NotEqual(sourceFile.FullPath, copiedFile.FullPath);
    }

    [Fact]
    public async Task Copy_WithOverwriteFalse_DoesNotOverwriteExisting()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        using var firstCopy = sourceFile.Copy(destDir.FullPath, overwrite: false);
        var firstCopyName = firstCopy.Name;

        // Act
        using var secondCopy = sourceFile.Copy(destDir.FullPath, overwrite: false);

        // Assert
        Assert.NotEqual(firstCopyName, secondCopy.Name);
        Assert.Contains("(1)", secondCopy.Name);
    }

    [Fact]
    public async Task Copy_WithOverwriteTrue_ReplacesExistingFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        // Create initial copy
        using var firstCopy = sourceFile.Copy(destDir.FullPath, overwrite: false);
        var originalName = firstCopy.Name;

        // Act
        using var secondCopy = sourceFile.Copy(destDir.FullPath, overwrite: true);

        // Assert
        Assert.Equal(originalName, secondCopy.Name);
    }

    [Fact]
    public async Task Copy_WithProgress_ReportsProgress()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");
        var progressReports = new List<StorageProgress>();
        var progress = new Progress<StorageProgress>(p => progressReports.Add(p));

        // Act
        using var copiedFile = sourceFile.Copy(destDir.FullPath, progress, overwrite: false);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.Last().BytesTransferred > 0);
    }

    [Fact]
    public async Task CopyAsync_WithValidDestination_CopiesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Document, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_async_dest");

        // Act
        using var copiedFile = await sourceFile.CopyAsync(destDir.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(File.Exists(copiedFile.FullPath));
        Assert.Equal(sourceFile.Information.Size, copiedFile.Information.Size);
    }

    [Fact]
    public async Task CopyAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Video, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("copy_cancel_dest");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
          await sourceFile.CopyAsync(destDir.FullPath, overwrite: false, cancellationToken: cts.Token));
    }

    #endregion

    #region Move Tests

    [Fact]
    public async Task Move_WithValidDestination_MovesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceFile.FullPath;
        var destDir = TestDirectory.CreateSubdirectory("move_dest");

        // Act
        using var movedFile = sourceFile.Move(destDir.FullPath, overwrite: false);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(movedFile.FullPath));
    }

    [Fact]
    public async Task Move_SameVolume_PerformsQuickMove()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("move_same_volume");
        var progressReports = new List<StorageProgress>();
        var progressReported = new TaskCompletionSource<bool>();
        var progress = new Progress<StorageProgress>(p =>
        {
            progressReports.Add(p);
            progressReported.SetResult(true);
        });

        // Act
        using var movedFile = sourceFile.Move(destDir.FullPath, progress, overwrite: false);

        // Wait for progress callback to execute (with timeout)
        var completedInTime = await Task.WhenAny(
            progressReported.Task, Task.Delay(1000, TestContext.Current.CancellationToken)) == progressReported.Task;

        // Assert
        Assert.True(completedInTime, "Progress callback did not execute within timeout");
        Assert.True(File.Exists(movedFile.FullPath));
        Assert.Single(progressReports); // Same volume move should report single progress update
    }

    [Fact]
    public async Task MoveAsync_WithValidDestination_MovesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceFile.FullPath;
        var destDir = TestDirectory.CreateSubdirectory("move_async_dest");

        // Act
        using var movedFile = await sourceFile.MoveAsync(destDir.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(movedFile.FullPath));
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_RemovesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        var filePath = sourceFile.FullPath;

        // Act
        sourceFile.Delete();

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var filePath = sourceFile.FullPath;

        // Act
        await sourceFile.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task Delete_DisposesObject()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        sourceFile.Delete();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sourceFile.Information);
    }

    #endregion

    #region Rename Tests

    [Fact]
    public void Rename_WithValidName_RenamesFile()
    {
        // Arrange
        var sourceFile = TestDirectory.CreateTestFile(ResourceType.Text);
        var originalPath = sourceFile.FullPath;
        var newName = "renamed.txt";

        // Act
        sourceFile.Rename(newName);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(Path.Combine(TestDirectory.FullPath, newName)));
        Assert.Equal(newName, sourceFile.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("invalid<>name.txt")]
    [InlineData("invalid|name.txt")]
    public async Task Rename_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sourceFile.Rename(invalidName!));
    }

    [Fact]
    public async Task RenameAsync_WithValidName_RenamesFile()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Document, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceFile.FullPath;
        var newName = "renamed_async.docx";

        // Act
        await sourceFile.RenameAsync(newName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(Path.Combine(TestDirectory.FullPath, newName)));
        Assert.Equal(newName, sourceFile.Name);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_WithStream_WritesDataToFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "write_test.bin");
        using var fileObj = new FileObject(filePath);
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        using var stream = new MemoryStream(testData);

        // Act
        fileObj.Write(stream, overwrite: true);

        // Assert
        var writtenData = File.ReadAllBytes(filePath);
        Assert.Equal(testData, writtenData);
    }

    [Fact]
    public void Write_WithProgress_ReportsProgress()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "write_progress_test.bin");
        using var fileObj = new FileObject(filePath);
        var testData = new byte[1024 * 100]; // 100 KB
        new Random().NextBytes(testData);
        using var stream = new MemoryStream(testData);
        var progressReports = new List<StorageProgress>();
        var progress = new Progress<StorageProgress>(p => progressReports.Add(p));

        // Act
        fileObj.Write(stream, progress, overwrite: true);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.Equal(testData.Length, progressReports.Last().BytesTransferred);
    }

    [Fact]
    public async Task WriteAsync_WithStream_WritesDataToFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "write_async_test.bin");
        using var fileObj = new FileObject(filePath);
        var testData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        using var stream = new MemoryStream(testData);

        // Act
        await fileObj.WriteAsync(stream, overwrite: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var writtenData = File.ReadAllBytes(filePath);
        Assert.Equal(testData, writtenData);
    }

    [Fact]
    public async Task WriteAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "write_cancel_test.bin");
        using var fileObj = new FileObject(filePath);
        var testData = new byte[1024 * 1024]; // 1 MB
        using var stream = new MemoryStream(testData);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
          await fileObj.WriteAsync(stream, overwrite: true, cancellationToken: cts.Token));
    }

    #endregion

    #region Property Tests

    [Fact]
    public async Task Name_ReturnsFileName()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var name = sourceFile.Name;

        // Assert
        Assert.Equal(Path.GetFileName(sourceFile.FullPath), name);
    }

    [Fact]
    public async Task Information_ReturnsFileInformation()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var info = sourceFile.Information;

        // Assert
        Assert.NotNull(info);
        Assert.True(info.Size > 0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task Dispose_DisposesObject()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        sourceFile.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sourceFile.Copy(TestDirectory.FullPath, overwrite: false));
    }

    [Fact]
    public async Task ThrowIfDisposed_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        sourceFile.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sourceFile.ThrowIfDisposed());
    }

    #endregion
}