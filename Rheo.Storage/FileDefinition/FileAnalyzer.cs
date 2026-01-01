using Rheo.Storage.FileDefinition.Models;

namespace Rheo.Storage.FileDefinition
{
    /// <summary>
    /// Provides static methods for analyzing files and identifying candidate definitions based on file header patterns
    /// and optional string checks.
    /// </summary>
    /// <remarks>This class is intended for internal use and is not thread-safe. All methods are static and do
    /// not maintain any internal state between calls.</remarks>
    public static class FileAnalyzer
    {
        private const int SCAN_WINDOW_SIZE = 8192; // 8KB scan window

        /// <summary>
        /// Analyzes the specified file and returns a list of analysis results based on candidate definitions found in
        /// the file header.
        /// </summary>
        /// <remarks>The confidence value for each result is calculated as a percentage of the total
        /// points assigned to all candidates. Results are ordered by descending score.</remarks>
        /// <param name="filePath">The full path to the file to analyze. The file must exist and be accessible.</param>
        /// <param name="checkStrings">true to perform additional string-based checks during analysis; otherwise, false. The default is true.</param>
        /// <returns>A list of AnalysisResult objects representing the analysis results for the file. The list is empty if the
        /// file does not exist, is inaccessible, or is empty.</returns>
        public static List<AnalysisResult> AnalyzeFile(string filePath, bool checkStrings = true)
        {
            if (!File.Exists(filePath))
                return [];

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
                return [];

            // Read file header
            byte[] headerBuffer = ReadFileHeader(fileInfo, SCAN_WINDOW_SIZE);
            
            // Get candidate definitions
            var candidateDefinitions = GetCandidateDefinitions(headerBuffer);
            
            // Score each candidate
            var results = new List<AnalysisResult>();
            var totalPoints = 0;

            foreach (var definition in candidateDefinitions)
            {
                int points = ScoreDefinition(definition, headerBuffer, filePath, checkStrings);
                
                if (points > 0)
                {
                    totalPoints += points;
                    results.Add(new AnalysisResult
                    {
                        Definition = definition,
                        Points = points,
                        Confidence = 0 // Will calculate after all scores
                    });
                }
            }

            // Handle case where no candidates matched but header is non-empty
            if (headerBuffer.Length > 0 && results.Count == 0)
            {
                int points = 100;

                // Use content type detector to determine if the file is text or binary
                var fallbackDefinition = ContentTypeDetector.CreateFallbackDefinition(headerBuffer, filePath);
                
                results.Add(new AnalysisResult
                {
                    Definition = fallbackDefinition,
                    Points = points, // Low score for fallback detection
                    Confidence = 100 // 100% of available points (since it's the only result)
                });

                totalPoints += points;
            }

            // Calculate confidence percentages
            foreach (var result in results)
            {
                result.Confidence = totalPoints > 0 ? result.Points * 100.0 / totalPoints : 0;
            }

            return [.. results.OrderByDescending(r => r.Points)];
        }

        private static byte[] ReadFileHeader(FileInfo fileInfo, int maxSize)
        {
            if (fileInfo.Length == 0)
                return [];

            // Create buffer and read file header
            var fileLength = fileInfo.Length;
            int size = (int)Math.Min(fileLength, maxSize);
            byte[] buffer = new byte[size];

            using var fileStream = fileInfo.OpenRead();
            fileStream.ReadExactly(buffer, 0, size);
            
            // Trim trailing null bytes to avoid false pattern matches
            return TrimTrailingNullBytes(buffer);
        }

        private static byte[] TrimTrailingNullBytes(byte[] buffer)
        {
            int lastNonNullIndex = buffer.Length - 1;
            
            while (lastNonNullIndex >= 0 && buffer[lastNonNullIndex] == 0)
            {
                lastNonNullIndex--;
            }
            
            if (lastNonNullIndex < 0)
                return [];
            
            if (lastNonNullIndex == buffer.Length - 1)
                return buffer;
            
            byte[] trimmed = new byte[lastNonNullIndex + 1];
            Array.Copy(buffer, trimmed, lastNonNullIndex + 1);
            return trimmed;
        }

        private static HashSet<Definition> GetCandidateDefinitions(byte[] headerBuffer)
        {
            var candidates = new HashSet<Definition>();
            var definitionsToCheck = new HashSet<Definition>();

            // Add catch-all definitions
            foreach (var map in FileDefinitions.AllPatternsByteMap[-1])
            {
                definitionsToCheck.Add(map.Definition);
            }

            // Scan for pattern matches and collect unique definitions
            for (int pos = 0; pos < headerBuffer.Length; pos++)
            {
                byte currentByte = headerBuffer[pos];
                
                if (!FileDefinitions.AllPatternsByteMap.TryGetValue(currentByte, out var patternMaps))
                    continue;
                
                foreach (var patternMap in patternMaps)
                {
                    if (patternMap.Pattern?.Position == pos)
                    {
                        definitionsToCheck.Add(patternMap.Definition);
                    }
                }
            }

            // Now validate each unique definition (all patterns must match)
            foreach (var definition in definitionsToCheck)
            {
                bool allPatternsMatch = true;
                
                foreach (var pattern in definition.Signature.Patterns)
                {
                    if (pattern.Position >= headerBuffer.Length ||
                        !MatchesPattern(headerBuffer, pattern.Position, pattern.Data))
                    {
                        allPatternsMatch = false;
                        break;
                    }
                }
                
                if (allPatternsMatch)
                {
                    candidates.Add(definition);
                }
            }

            return candidates;
        }

        private static bool MatchesPattern(byte[] buffer, int offset, byte[] pattern)
        {
            if (offset + pattern.Length > buffer.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (buffer[offset + i] != pattern[i])
                    return false;
            }

            return true;
        }

        private static int ScoreDefinition(Definition definition, byte[] headerBuffer, string filePath, bool checkStrings)
        {
            int points = 0;

            // Score patterns
            foreach (var pattern in definition.Signature.Patterns)
            {
                if (pattern.Position < headerBuffer.Length &&
                    MatchesPattern(headerBuffer, pattern.Position, pattern.Data))
                {
                    // Weight by position and length
                    int weight = pattern.Position == 0 ? 1000 : 100;
                    points += pattern.Data.Length * weight;
                }
                else
                {
                    // Pattern mismatch - this definition is invalid
                    return 0;
                }
            }

            // Score strings if enabled
            if (checkStrings && points > 0 && definition.Signature.Strings.Count > 0)
            {
                byte[] fileBuffer = File.ReadAllBytes(filePath);
                
                foreach (var stringBytes in definition.Signature.Strings)
                {
                    if (ContainsSequence(fileBuffer, stringBytes))
                    {
                        points += stringBytes.Length * 500;
                    }
                }
            }

            return points;
        }

        private static bool ContainsSequence(byte[] source, byte[] sequence)
        {
            for (int i = 0; i <= source.Length - sequence.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (source[i + j] != sequence[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return true;
            }
            return false;
        }
    }
}