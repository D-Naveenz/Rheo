using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Handling;

[Feature("FileObject")]
[Category("Storage Operations")]
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
    public void Copy_WithValidDestination_CopiesFile()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        // Act
        using var copiedFile = sourceFile.Copy(destDir.FullPath, overwrite: false);

        // Assert
        Assert.True(File.Exists(copiedFile.FullPath));
        Assert.Equal(sourceFile.Information.Size, copiedFile.Information.Size);
        Assert.NotEqual(sourceFile.FullPath, copiedFile.FullPath);
    }

    [Fact]
    public void Copy_WithOverwriteFalse_DoesNotOverwriteExisting()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        string firstCopyName;
        
        // Create initial copy and dispose immediately
        using (var firstCopy = sourceFile.Copy(destDir.FullPath, overwrite: false))
        {
            firstCopyName = firstCopy.Name;
        } // firstCopy is disposed here

        // Act
        using var secondCopy = sourceFile.Copy(destDir.FullPath, overwrite: false);

        // Assert
        Assert.NotEqual(firstCopyName, secondCopy.Name);
        Assert.Contains("(1)", secondCopy.Name);
    }

    [Fact]
    public void Copy_WithOverwriteTrue_ReplacesExistingFile()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var destDir = TestDirectory.CreateSubdirectory("copy_dest");

        string originalName;
        
        // Create initial copy and dispose immediately
        using (var firstCopy = sourceFile.Copy(destDir.FullPath, overwrite: false))
        {
            originalName = firstCopy.Name;
        } // firstCopy is disposed here

        // Act
        using var secondCopy = sourceFile.Copy(destDir.FullPath, overwrite: true);

        // Assert
        Assert.Equal(originalName, secondCopy.Name);
    }

    [Fact]
    public void Copy_WithProgress_ReportsProgress()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Image);
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
        using var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Document, cancellationToken: TestContext.Current.CancellationToken);
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
        using var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Video, cancellationToken: TestContext.Current.CancellationToken);
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
    public void Move_WithValidDestination_MovesFile()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var originalPath = sourceFile.FullPath;
        var originalName = sourceFile.Name;
        var destDir = TestDirectory.CreateSubdirectory("move_dest");

        // Act
        sourceFile.Move(destDir.FullPath, overwrite: false);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(sourceFile.FullPath));
        Assert.Equal(originalName, sourceFile.Name);
        Assert.Contains("move_dest", sourceFile.FullPath);
    }

    [Fact]
    public async Task Move_SameVolume_PerformsQuickMove()
    {
        // Arrange
        using var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("move_same_volume");
        var progressReports = new List<StorageProgress>();
        var progressReported = new TaskCompletionSource<bool>();
        var progress = new Progress<StorageProgress>(p =>
        {
            progressReports.Add(p);
            progressReported.TrySetResult(true);
        });

        // Act
        sourceFile.Move(destDir.FullPath, progress, overwrite: false);

        // Wait for progress callback to execute (with timeout)
        var completedInTime = await Task.WhenAny(
            progressReported.Task, Task.Delay(1000, TestContext.Current.CancellationToken)) == progressReported.Task;

        // Assert
        Assert.True(completedInTime, "Progress callback did not execute within timeout");
        Assert.True(File.Exists(sourceFile.FullPath));
        Assert.Single(progressReports); // Same volume move should report single progress update
    }

    [Fact]
    public async Task MoveAsync_WithValidDestination_MovesFile()
    {
        // Arrange
        using var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Image, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceFile.FullPath;
        var destDir = TestDirectory.CreateSubdirectory("move_async_dest");

        // Act
        await sourceFile.MoveAsync(destDir.FullPath, overwrite: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(sourceFile.FullPath));
        Assert.Contains("move_async_dest", sourceFile.FullPath);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_RemovesFile()
    {
        // Arrange
        string filePath;
        using (var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text))
        {
            filePath = sourceFile.FullPath;
            
            // Act
            sourceFile.Delete();
        }

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        string filePath;
        using (var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken))
        {
            filePath = sourceFile.FullPath;
            
            // Act
            await sourceFile.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Delete_DisposesObject()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);

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
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);
        var originalPath = sourceFile.FullPath;
        var newName = "renamed.txt";

        // Act
        sourceFile.Rename(newName);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(sourceFile.FullPath));
        Assert.Equal(newName, sourceFile.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("invalid<>name.txt")]
    [InlineData("invalid|name.txt")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sourceFile.Rename(invalidName!));
    }

    [Fact]
    public async Task RenameAsync_WithValidName_RenamesFile()
    {
        // Arrange
        using var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Document, cancellationToken: TestContext.Current.CancellationToken);
        var originalPath = sourceFile.FullPath;
        var newName = "renamed_async.docx";

        // Act
        await sourceFile.RenameAsync(newName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(sourceFile.FullPath));
        Assert.Equal(newName, sourceFile.Name);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Name_ReturnsFileName()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);

        // Act
        var name = sourceFile.Name;

        // Assert
        Assert.Equal(Path.GetFileName(sourceFile.FullPath), name);
    }

    [Fact]
    public void Information_ReturnsFileInformation()
    {
        // Arrange
        using var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

        // Act
        var info = sourceFile.Information;

        // Assert
        Assert.NotNull(info);
        Assert.True(info.Size > 0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_DisposesObject()
    {
        // Arrange
        var sourceFile = TestDirectory.CreateTemplateFile(ResourceType.Text);

        // Act
        sourceFile.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sourceFile.Copy(TestDirectory.FullPath, overwrite: false));
    }

    [Fact]
    public async Task ThrowIfDisposed_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);
        sourceFile.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sourceFile.ThrowIfDisposed());
    }

    #endregion
}