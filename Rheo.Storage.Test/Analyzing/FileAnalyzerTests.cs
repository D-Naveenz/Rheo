using Rheo.Storage.Analyzing;
using Rheo.Storage.Test.Extensions;

namespace Rheo.Storage.Test.Analyzing
{
    [Feature("FileAnalyzer")]
    [Category("Default Tests")]
    public class FileAnalyzerTests(ITestOutputHelper output, TestDirectoryFixture fixture) : SafeStorageTestClass(output, fixture)
    {
        [Fact]
        public void AnalyzeFile_WithNonExistentFile_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(TestDirectory.FullPath, "nonexistent.bin");

            // Act
            var result = FileAnalyzer.AnalyzeFile(nonExistentPath);

            // Assert
            Assert.Empty(result.Definitions);
        }

        [Fact]
        public void AnalyzeFile_WithEmptyFile_ReturnsEmptyList()
        {
            // Arrange
            var emptyFilePath = Path.Combine(TestDirectory.FullPath, "empty.bin");
            File.WriteAllBytes(emptyFilePath, []);

            // Act
            var result = FileAnalyzer.AnalyzeFile(emptyFilePath);

            // Assert
            Assert.Empty(result.Definitions);
        }

        [Fact]
        public void AnalyzeFile_WithTextFile_ReturnsResults()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Text);

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
        public void AnalyzeFile_WithPngImage_ReturnsResults()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

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
        public void AnalyzeFile_WithDocument_ReturnsResults()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Document);

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public void AnalyzeFile_WithVideo_ReturnsResultsAsync()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Video);

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            Assert.NotEmpty(result.Definitions);
            
            var topResult = result.Definitions.First();
            Assert.NotNull(topResult.Subject);
            Assert.True(topResult.Value > 0);
        }

        [Fact]
        public void AnalyzeFile_ResultsAreOrderedByPointsAsync()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

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
        public void AnalyzeFile_ConfidencesSumTo100Percent()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

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
        public void AnalyzeFile_WithCheckStringsFalse_StillReturnsResults()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

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
            var zipPath = Path.Combine(TestDirectory.FullPath, "test.zip");
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
            var customPath = Path.Combine(TestDirectory.FullPath, "custom.bin");
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
        public void AnalyzeFile_WithVariousResourceTypes_ReturnsValidResults(ResourceType resourceType)
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(resourceType);

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
        public void AnalyzeFile_TopResultHasHighestConfidence()
        {
            // Arrange
            var testFile = TestDirectory.CreateTemplateFile(ResourceType.Image);

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
            var largePath = Path.Combine(TestDirectory.FullPath, "large.bin");
            byte[] largeData = new byte[10 * 1024]; // 10KB

            // Add PNG data
            var (data, _) = TestFileProvider.GetImageFile(TestDirectory.FullPath);
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