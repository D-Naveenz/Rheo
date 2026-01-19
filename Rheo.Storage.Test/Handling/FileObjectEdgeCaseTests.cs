using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Handling;

[Category("FileObject")]
[Category("Edge Case Tests")]
public class FileObjectEdgeCaseTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
{
    [Fact]
    public void Copy_ToSameLocation_CreatesNumberedCopy()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "original.txt");
        File.WriteAllText(filePath, "test content");
        using var fileObj = new FileObject(filePath);

        // Act
        using var copy1 = fileObj.Copy(TestDirectory.FullPath, overwrite: false);
        using var copy2 = fileObj.Copy(TestDirectory.FullPath, overwrite: false);

        // Assert
        Assert.Contains("(1)", copy1.Name);
        Assert.Contains("(2)", copy2.Name);
    }

    [Fact]
    public async Task CopyAsync_MultipleSimultaneous_AllSucceed()
    {
        // Arrange
        var sourceFile = await TestDirectory.CreateTemplateFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
        var destDir = TestDirectory.CreateSubdirectory("concurrent_copy");

        // Act
        var copyTasks = Enumerable.Range(0, 5).Select(i =>
          sourceFile.CopyAsync(Path.Combine(destDir.FullPath, $"copy_{i}"), overwrite: true, cancellationToken: TestContext.Current.CancellationToken)
        ).ToList();

        var results = await Task.WhenAll(copyTasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, r => Assert.True(File.Exists(r.FullPath)));

        // Cleanup
        foreach (var result in results)
        {
            result.Dispose();
        }
    }

    [Fact]
    public void Write_EmptyData_CreatesEmptyFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "empty.bin");
        using var fileObj = new FileObject(filePath);
        byte[] emptyData = [];

        // Act
        fileObj.Write(emptyData);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.Equal(0, fileObj.Information.Size);
    }

    [Fact]
    public void GetBufferSize_ForSmallFile_ReturnsMinimumBuffer()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "small.txt");
        File.WriteAllText(filePath, "tiny");
        using var fileObj = new FileObject(filePath);

        // Act
        var bufferSize = fileObj.GetBufferSize();

        // Assert
        Assert.True(bufferSize >= 1024); // MIN_BUFFER_SIZE
    }

    [Fact]
    public void Move_ToNonExistentDirectory_CreatesDirectory()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "move_test.txt");
        File.WriteAllText(filePath, "content");
        using var fileObj = new FileObject(filePath);
        var newDir = Path.Combine(TestDirectory.FullPath, "new", "nested", "dir");

        // Act
        fileObj.Move(newDir, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(newDir));
        Assert.True(File.Exists(fileObj.FullPath));
        Assert.Contains("nested", fileObj.FullPath);
    }

    [Fact]
    public void Rename_ToExistingFileName_ResolvesConflicts()
    {
        // Arrange
        var file1Path = Path.Combine(TestDirectory.FullPath, "file1.txt");
        var file2Path = Path.Combine(TestDirectory.FullPath, "file2.txt");
        File.WriteAllText(file1Path, "content1");
        File.WriteAllText(file2Path, "content2");
        using var fileObj1 = new FileObject(file1Path);

        // Act
        fileObj1.Rename("file2.txt");

        // Assert
        Assert.Equal("file2 (1).txt", fileObj1.Name);
        Assert.True(File.Exists(fileObj1.FullPath));
    }

    [Fact]
    public async Task WriteAsync_LargeFile_CompletesSuccessfully()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "large.bin");
        using var fileObj = new FileObject(filePath);
        var largeData = new byte[5 * 1024 * 1024]; // 5 MB
        new Random(42).NextBytes(largeData);
        using var stream = new MemoryStream(largeData);

        // Act
        await fileObj.WriteAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(largeData.Length, fileObj.Information.Size);
    }

    [Fact]
    public void Move_UpdatesInformation()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "move_info_test.txt");
        File.WriteAllText(filePath, "content");
        using var fileObj = new FileObject(filePath);
        var originalPath = fileObj.FullPath;
        var destDir = TestDirectory.CreateSubdirectory("move_dest");

        // Act
        fileObj.Move(destDir.FullPath, overwrite: false);

        // Assert
        Assert.NotEqual(originalPath, fileObj.FullPath);
        Assert.Equal(destDir.FullPath, fileObj.ParentDirectory);
    }

    [Fact]
    public void Rename_UpdatesInformation()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "rename_test.txt");
        File.WriteAllText(filePath, "content");
        using var fileObj = new FileObject(filePath);
        var originalName = fileObj.Name;
        var newName = "renamed.txt";

        // Act
        fileObj.Rename(newName);

        // Assert
        Assert.NotEqual(originalName, fileObj.Name);
        Assert.Equal(newName, fileObj.Name);
    }
}