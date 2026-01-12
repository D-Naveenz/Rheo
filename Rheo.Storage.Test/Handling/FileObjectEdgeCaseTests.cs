using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Handling;

[Trait(TestTraits.Feature, "FileObject")]
[Trait(TestTraits.Category, "Edge Case Tests")]
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
        var sourceFile = await TestDirectory.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);
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
    public void Write_EmptyStream_CreatesEmptyFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "empty.bin");
        using var fileObj = new FileObject(filePath);
        using var emptyStream = new MemoryStream();

        // Act
        fileObj.Write(emptyStream, overwrite: true);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.Equal(0, new FileInfo(filePath).Length);
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
        using var movedFile = fileObj.Move(newDir, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(newDir));
        Assert.True(File.Exists(movedFile.FullPath));
    }

    [Fact]
    public void Rename_ToExistingFileName_Resolves_Conflicts()
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
        Assert.True(File.Exists(Path.Combine(TestDirectory.FullPath, "file2 (1).txt")));
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
        await fileObj.WriteAsync(stream, overwrite: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var fileInfo = new FileInfo(filePath);
        Assert.Equal(largeData.Length, fileInfo.Length);
    }

    [Fact]
    public void CopyFrom_UpdatesInformation()
    {
        // Arrange
        var file1Path = Path.Combine(TestDirectory.FullPath, "file1.txt");
        var file2Path = Path.Combine(TestDirectory.FullPath, "file2.txt");
        File.WriteAllText(file1Path, "small");
        File.WriteAllText(file2Path, "much larger content here");

        using var fileObj1 = new FileObject(file1Path);
        using var fileObj2 = new FileObject(file2Path);
        var originalSize = fileObj1.Information.Size;

        // Act
        fileObj1.CopyFrom(fileObj2);

        // Assert
        Assert.NotEqual(originalSize, fileObj1.Information.Size);
        Assert.Equal(file2Path, fileObj1.FullPath);
    }
}