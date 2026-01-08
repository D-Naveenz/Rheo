using Rheo.Storage.Analysing;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.FileDefinitions
{
    [Trait(TestTraits.Feature, "FileAnalyzer")]
    [Trait(TestTraits.Category, "Default Tests")]
    public class FileAnalyzerTests(TestDirectoryFixture fileAnalyzerFixture) : IClassFixture<TestDirectoryFixture>
    {
        private readonly TestDirectoryFixture _fileAnalyzerFixture = fileAnalyzerFixture;
        
        private TestDirectory TestDir => _fileAnalyzerFixture.TestDir;

        [Fact]
        public void AnalyzeFile_WithNonExistentFile_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDir.FullPath, "nonexistent.bin");

            // Act
            var result = FileAnalyzer.AnalyzeFile(nonExistentPath);

            // Assert
            Assert.Empty(result.Definitions);
        }

        [Fact]
        public void AnalyzeFile_WithEmptyFile_ReturnsEmptyList()
        {
            // Arrange
            var emptyFilePath = Path.Combine(TestDir.FullPath, "empty.bin");
            File.WriteAllBytes(emptyFilePath, []);

            // Act
            var result = FileAnalyzer.AnalyzeFile(emptyFilePath);

            // Assert
            Assert.Empty(result.Definitions);
        }

        [Fact]
        public async Task AnalyzeFile_WithTextFile_ReturnsResults()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Text, 
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);

            // Text files should have at least one match
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public async Task AnalyzeFile_WithPngImage_ReturnsResultsAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Extensions.First();
            
            // PNG files have signature: 89 50 4E 47 0D 0A 1A 0A
            Assert.NotNull(topResult.Subject);
            Assert.Contains("png", topResult.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.True(topResult.Value > 50, "PNG should have high confidence due to strong signature");
        }

        [Fact]
        public async Task AnalyzeFile_WithDocument_ReturnsResultsAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Document,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public async Task AnalyzeFile_WithVideo_ReturnsResultsAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Video,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public async Task AnalyzeFile_ResultsAreOrderedByPointsAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            var defList = result.Definitions.ToList();
            if (defList.Count > 1)
            {
                for (int i = 0; i < defList.Count - 1; i++)
                {
                    Assert.True(defList[i].Value >= defList[i + 1].Value,
                        "Results should be ordered by points in descending order");
                }
            }
        }

        [Fact]
        public async Task AnalyzeFile_ConfidencesSumTo100PercentAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            var defList = result.Definitions.ToList();
            if (defList.Count > 0)
            {
                var totalConfidence = defList.Sum(r => r.Value);
                Assert.True(Math.Abs(totalConfidence - 100.0) < 0.01,
                    $"Total confidence should be ~100%, but was {totalConfidence}%");
            }
        }

        [Fact]
        public async Task AnalyzeFile_WithCheckStringsFalse_StillReturnsResultsAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath, checkStrings: false);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            // Results might differ from string-enabled analysis, but should still work
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public void AnalyzeFile_WithKnownSignature_DetectsCorrectly()
        {
            // Arrange: Create a file with known ZIP signature (PK header)
            var zipPath = Path.Combine(TestDir.FullPath, "test.zip");
            byte[] zipSignature = [0x50, 0x4B, 0x03, 0x04]; // ZIP/JAR/DOCX signature
            File.WriteAllBytes(zipPath, zipSignature);

            // Act
            var result = FileAnalyzer.AnalyzeFile(zipPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            
            // Should detect as ZIP-based format
            Assert.True(
                topResult.Subject.Extensions.Any(ext => 
                    ext.Equals("zip", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals("jar", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals("docx", StringComparison.OrdinalIgnoreCase)),
                "Should detect ZIP-based format");
        }

        [Fact]
        public void AnalyzeFile_WithCustomBinaryPattern_DetectsOrReturnsEmpty()
        {
            // Arrange: Create file with custom binary pattern
            var customPath = Path.Combine(TestDir.FullPath, "custom.bin");
            byte[] customData = [0x00, 0xFF, 0x7A, 0x3C, 0x5D, 0xA1, 0x42, 0x99];
            File.WriteAllBytes(customPath, customData);

            // Act
            var result = FileAnalyzer.AnalyzeFile(customPath);

            // Assert
            // Custom patterns might not match any known definitions
            // This test validates that the analyzer handles unknown patterns gracefully
            Assert.NotNull(result); // Should return empty list, not throw
        }

        [Theory]
        [InlineData(ResourceType.Text)]
        [InlineData(ResourceType.Image)]
        [InlineData(ResourceType.Binary)]
        [InlineData(ResourceType.Document)]
        [InlineData(ResourceType.Video)]
        public async Task AnalyzeFile_WithVariousResourceTypes_ReturnsValidResultsAsync(ResourceType resourceType)
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                resourceType,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            // All valid files should return some analysis (even if empty for unknown types)
            Assert.NotNull(result);
            
            if (result.Definitions.Count != 0)
            {
                // If results exist, validate their structure
                foreach (var confidence in result.Definitions)
                {
                    Assert.NotNull(confidence.Subject);
                    Assert.True(confidence.Value >= 0 && confidence.Value <= 100);
                }
            }
        }

        [Fact]
        public async Task AnalyzeFile_TopResultHasHighestConfidenceAsync()
        {
            // Arrange
            var testFile = await TestDir.CreateTestFileAsync(
                ResourceType.Image,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            if (result.Definitions.Count > 1)
            {
                var topResult = result.Definitions.First();
                foreach (var otherResult in result.Definitions.Skip(1))
                {
                    Assert.True(topResult.Value >= otherResult.Value,
                        "Top result should have highest confidence");
                }
            }
        }

        [Fact]
        public void AnalyzeFile_WithLargeFile_HandlesGracefully()
        {
            // Arrange: Create a larger file (simulating real-world scenario)
            var largePath = Path.Combine(TestDir.FullPath, "large.bin");
            byte[] largeData = new byte[10 * 1024]; // 10KB

            // Add PNG data
            var (data, _) = TestFileProvider.GetImageFile(TestDir.FullPath);
            Array.Copy(data, 0, largeData, 0, Math.Min(data.Length, largeData.Length));
            
            File.WriteAllBytes(largePath, largeData);

            // Act
            var result = FileAnalyzer.AnalyzeFile(largePath);

            // Assert
            Assert.NotEmpty(result.Extensions);
            
            var topResult = result.Extensions.First();
            Assert.Contains("png", topResult.Subject, StringComparison.OrdinalIgnoreCase);
        }
    }
}