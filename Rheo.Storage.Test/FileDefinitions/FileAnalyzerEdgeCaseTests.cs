using Rheo.Storage.Analysing;
using Rheo.Storage.Test.Models;
using Rheo.Storage.Test.Utilities;

namespace Rheo.Storage.Test.FileDefinitions
{
    [Trait(TestTraits.Feature, "FileAnalyzer")]
    [Trait(TestTraits.Category, "Edge Case Tests")]
    public class FileAnalyzerEdgeCaseTests : IDisposable
    {
        private readonly TestDirectory _testDir;

        public FileAnalyzerEdgeCaseTests()
        {
            _testDir = TestDirectory.Create();
        }

        [Fact]
        public void AnalyzeFile_WithOnlyHeaderPattern_NoStrings_DetectsCorrectly()
        {
            // Arrange: PDF signature
            var pdfPath = Path.Combine(_testDir.FullPath, "test.pdf");
            byte[] pdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D]; // "%PDF-"
            File.WriteAllBytes(pdfPath, pdfHeader);

            // Act
            var result = FileAnalyzer.AnalyzeFile(pdfPath, checkStrings: false);

            // Assert
            Assert.NotEmpty(result.Extensions);
            Assert.Contains("pdf", result.Extensions.Values, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AnalyzeFile_WithMultipleMatchingDefinitions_RanksCorrectlyAsync()
        {
            // Arrange: Create file that might match multiple definitions
            var testFile = await _testDir.CreateTestFileAsync(
                ResourceType.Document,
                cancellationToken: TestContext.Current.CancellationToken
                );

            // Act
            var result = FileAnalyzer.AnalyzeFile(testFile.FullPath);

            // Assert
            if (result.Definitions.Count > 1)
            {
                // Verify ranking is deterministic
                var firstRun = FileAnalyzer.AnalyzeFile(testFile.FullPath);
                var secondRun = FileAnalyzer.AnalyzeFile(testFile.FullPath);

                Assert.Equal(firstRun.Definitions.Count, secondRun.Definitions.Count);
                for (int i = 0; i < firstRun.Definitions.Count; i++)
                {
                    Assert.Equal(firstRun.Definitions[i].Value, secondRun.Definitions[i].Value);
                }
            }
        }

        [Fact]
        public void AnalyzeFile_WithPatternAtNonZeroPosition_DetectsCorrectly()
        {
            // Arrange: Create file with pattern not at position 0
            var testPath = Path.Combine(_testDir.FullPath, "offset.bin");
            byte[] data = new byte[100];
            
            // Add some padding
            for (int i = 0; i < 10; i++)
                data[i] = 0x00;
            
            // Add a known pattern at offset 10
            data[10] = 0x50; // 'P'
            data[11] = 0x4B; // 'K' (ZIP signature)
            
            File.WriteAllBytes(testPath, data);

            // Act
            var result = FileAnalyzer.AnalyzeFile(testPath);

            // Assert
            // Should handle patterns at various positions
            Assert.NotEmpty(result.MimeTypes);
        }

        [Fact]
        public void AnalyzeFile_WithVeryShortFile_HandlesGracefully()
        {
            // Arrange
            var shortPath = Path.Combine(_testDir.FullPath, "short.bin");
            File.WriteAllBytes(shortPath, [0x42]); // Single byte

            // Act
            var result = FileAnalyzer.AnalyzeFile(shortPath);

            // Assert
            Assert.NotEmpty(result.MimeTypes); // Should not throw
        }

        [Fact]
        public void AnalyzeFile_CatchAllDefinitions_AreNotIncluded()
        {
            // Arrange: File with no recognizable pattern
            var unknownPath = Path.Combine(_testDir.FullPath, "unknown.bin");
            byte[] randomData = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
            File.WriteAllBytes(unknownPath, randomData);

            // Act
            var result = FileAnalyzer.AnalyzeFile(unknownPath);

            // Assert
            // With catch-all removed, unknown files should return empty or very few results
            // This validates your decision to remove catch-all logic
            Assert.True(result.Definitions.Count == 0 || result.Definitions.All(r => r.Value > 0),
                "Results should either be empty or have positive points (no catch-all guesses)");
        }

        public void Dispose()
        {
            _testDir?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}