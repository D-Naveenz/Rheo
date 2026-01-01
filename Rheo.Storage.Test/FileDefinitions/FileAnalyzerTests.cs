using Rheo.Storage.FileDefinition;
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
            var results = FileAnalyzer.AnalyzeFile(nonExistentPath);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void AnalyzeFile_WithEmptyFile_ReturnsEmptyList()
        {
            // Arrange
            var emptyFilePath = Path.Combine(TestDir.FullPath, "empty.bin");
            File.WriteAllBytes(emptyFilePath, []);

            // Act
            var results = FileAnalyzer.AnalyzeFile(emptyFilePath);

            // Assert
            Assert.Empty(results);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(results);
            
            // Text files should have at least one match
            var topResult = results.First();
            Assert.NotNull(topResult.Definition);
            Assert.True(topResult.Points > 0);
            Assert.True(topResult.Confidence > 0);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(results);
            
            var topResult = results.First();
            
            // PNG files have signature: 89 50 4E 47 0D 0A 1A 0A
            Assert.NotNull(topResult.Definition);
            Assert.Contains("png", topResult.Definition.Extensions, StringComparer.OrdinalIgnoreCase);
            Assert.True(topResult.Confidence > 50, "PNG should have high confidence due to strong signature");
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(results);
            
            var topResult = results.First();
            Assert.NotNull(topResult.Definition);
            Assert.True(topResult.Points > 0);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(results);
            
            var topResult = results.First();
            Assert.NotNull(topResult.Definition);
            Assert.True(topResult.Points > 0);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            if (results.Count > 1)
            {
                for (int i = 0; i < results.Count - 1; i++)
                {
                    Assert.True(results[i].Points >= results[i + 1].Points,
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            if (results.Count > 0)
            {
                var totalConfidence = results.Sum(r => r.Confidence);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath, checkStrings: false);

            // Assert
            Assert.NotEmpty(results);
            
            // Results might differ from string-enabled analysis, but should still work
            var topResult = results.First();
            Assert.NotNull(topResult.Definition);
            Assert.True(topResult.Points > 0);
        }

        [Fact]
        public void AnalyzeFile_WithKnownSignature_DetectsCorrectly()
        {
            // Arrange: Create a file with known ZIP signature (PK header)
            var zipPath = Path.Combine(TestDir.FullPath, "test.zip");
            byte[] zipSignature = [0x50, 0x4B, 0x03, 0x04]; // ZIP/JAR/DOCX signature
            File.WriteAllBytes(zipPath, zipSignature);

            // Act
            var results = FileAnalyzer.AnalyzeFile(zipPath);

            // Assert
            Assert.NotEmpty(results);
            
            var topResult = results.First();
            Assert.NotNull(topResult.Definition);
            
            // Should detect as ZIP-based format
            Assert.True(
                topResult.Definition.Extensions.Any(ext => 
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
            var results = FileAnalyzer.AnalyzeFile(customPath);

            // Assert
            // Custom patterns might not match any known definitions
            // This test validates that the analyzer handles unknown patterns gracefully
            Assert.NotNull(results); // Should return empty list, not throw
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            // All valid files should return some analysis (even if empty for unknown types)
            Assert.NotNull(results);
            
            if (results.Count != 0)
            {
                // If results exist, validate their structure
                foreach (var result in results)
                {
                    Assert.NotNull(result.Definition);
                    Assert.True(result.Points >= 0);
                    Assert.True(result.Confidence >= 0 && result.Confidence <= 100);
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
            var results = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            if (results.Count > 1)
            {
                var topResult = results.First();
                foreach (var otherResult in results.Skip(1))
                {
                    Assert.True(topResult.Confidence >= otherResult.Confidence,
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
            var results = FileAnalyzer.AnalyzeFile(largePath);

            // Assert
            Assert.NotEmpty(results);
            
            var topResult = results.First();
            Assert.Contains("png", topResult.Definition.Extensions, StringComparer.OrdinalIgnoreCase);
        }
    }
}