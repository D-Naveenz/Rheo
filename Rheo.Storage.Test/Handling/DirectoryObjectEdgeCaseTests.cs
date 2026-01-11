using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.Handling;

[Trait(TestTraits.Feature, "DirectoryObject")]
[Trait(TestTraits.Category, "Edge Case Tests")]
public class DirectoryObjectEdgeCaseTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
{
    [Fact]
    public void Copy_EmptyDirectory_CreatesEmptyDirectory()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("empty_source");
        var destParent = TestDirectory.CreateSubdirectory("empty_dest");

        // Act
        using var copiedDir = sourceDir.Copy(destParent.FullPath, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(copiedDir.FullPath));
        Assert.Empty(copiedDir.GetFiles());
    }

    [Fact]
    public async Task Copy_DeepHierarchy_PreservesStructure()
    {
        // Arrange
        var level1 = TestDirectory.CreateSubdirectory("level1");
        var level2 = level1.CreateSubdirectory("level2");
        var level3 = level2.CreateSubdirectory("level3");
        await level3.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDirectory.CreateSubdirectory("deep_dest");

        // Act
        using var copiedDir = TestDirectory.Copy(destParent.FullPath, overwrite: false);

        // Assert
        var deepestFile = copiedDir.GetFiles("*", SearchOption.AllDirectories);
        Assert.Single(deepestFile);
        Assert.Contains("level3", deepestFile[0]);
    }

    [Fact]
    public void Copy_ToSameLocation_CreatesNumberedCopy()
    {
        // Arrange
        var parentDir = TestDirectoryParent.FullPath;

        // Act
        using var copy1 = TestDirectory.Copy(parentDir, overwrite: false);
        using var copy2 = TestDirectory.Copy(parentDir, overwrite: false);

        // Assert
        Assert.Contains("(1)", copy1.Name);
        Assert.Contains("(2)", copy2.Name);
    }

    [Fact]
    public void Move_ToNonExistentPath_CreatesPath()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory();
        var newPath = Path.Combine(TestDirectory.FullPath, "new", "nested", "path");

        // Act
        using var movedDir = sourceDir.Move(newPath, overwrite: false);

        // Assert
        Assert.True(Directory.Exists(movedDir.ParentDirectory));
    }

    [Fact]
    public async Task CopyAsync_WithCancellation_CleansUpPartialCopy()
    {
        // Arrange
        // Create multiple large files to ensure operation takes time
        for (int i = 0; i < 5; i++)
        {
            await TestDirectory.CreateTestFileAsync(ResourceType.Video, cancellationToken: TestContext.Current.CancellationToken);
        }

        var destParent = TestDirectory.CreateSubdirectory("cancel_dest");
        using var cts = new CancellationTokenSource();

        // Track when the copy actually starts working
        var copyStarted = new TaskCompletionSource<bool>();
        var progressReports = 0;
        var progress = new Progress<StorageProgress>(_ =>
        {
            if (Interlocked.Increment(ref progressReports) == 1)
            {
                // First progress report means copy has started
                copyStarted.TrySetResult(true);
            }
        });

        // Act
        var copyTask = TestDirectory.CopyAsync(destParent.FullPath, progress, overwrite: false, cancellationToken: cts.Token);

        // Wait for copy to actually start (with timeout)
        var started = await Task.WhenAny(copyStarted.Task, Task.Delay(2000, TestContext.Current.CancellationToken)) == copyStarted.Task;
        
        if (started)
        {
            // Cancel after copy has started but before it completes
            await Task.Delay(10, TestContext.Current.CancellationToken); // Let it do a bit more work
            cts.Cancel();
        }
        else
        {
            // Fallback: cancel after a short delay if progress never reported
            cts.Cancel();
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await copyTask);

        // Verify cleanup - destination should be removed
        var potentialDestPath = Path.Combine(destParent.FullPath, TestDirectory.Name);
        Assert.False(Directory.Exists(potentialDestPath));
    }

    [Fact]
    public void GetFile_FromSubdirectory_ReturnsCorrectFile()
    {
        // Arrange
        var filePath = Path.Combine(TestDirectory.FullPath, "test.txt");
        File.WriteAllText(filePath, "content");

        // Act
        using var fileObj = TestDirectoryParent.GetFile(Path.Combine(TestDirectory.Name, "test.txt"));

        // Assert
        Assert.Equal(filePath, fileObj.FullPath);
    }

    [Fact]
    public void GetDirectory_FromNestedPath_ReturnsCorrectDirectory()
    {
        // Arrange
        var level1 = TestDirectory.CreateSubdirectory("level1");
        var level2 = level1.CreateSubdirectory("level2");

        // Act
        using var dirObj = TestDirectory.GetDirectory(Path.Combine("level1", "level2"));

        // Assert
        Assert.Equal(level2.FullPath, dirObj.FullPath);
    }

    [Fact]
    public async Task Delete_WithReadOnlyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var testFile = await TestDirectory.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        // Make file read-only
        var fileInfo = new FileInfo(testFile.FullPath)
        {
            IsReadOnly = true
        };

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => TestDirectory.Delete());
        }
        finally
        {
            // Cleanup
            fileInfo.IsReadOnly = false;
        }
    }

    [Fact]
    public void Rename_WithLongName_HandlesCorrectly()
    {
        // Arrange
        var longName = new string('a', 100); // Long but valid name

        // Act
        TestDirectory.Rename(longName);

        // Assert
        Assert.Equal(longName, TestDirectory.Name);
        Assert.True(Directory.Exists(TestDirectory.FullPath));
    }

    [Fact]
    public async Task Move_WithOverwrite_ReplacesDestination()
    {
        // Arrange
        var sourceDir = TestDirectory.CreateSubdirectory("move_overwrite_source");
        await sourceDir.CreateTestFileAsync(ResourceType.Text, cancellationToken: TestContext.Current.CancellationToken);

        var destParent = TestDirectory.CreateSubdirectory("move_overwrite_parent");
        var existingDir = destParent.CreateSubdirectory(sourceDir.Name);
        await existingDir.CreateTestFileAsync(ResourceType.Binary, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        using var movedDir = sourceDir.Move(destParent.FullPath, overwrite: true);

        // Assert
        Assert.True(Directory.Exists(movedDir.FullPath));
        // Should only have files from source
        Assert.Single(movedDir.GetFiles());
    }
}