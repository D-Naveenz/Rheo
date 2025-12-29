using Rheo.Storage.MIME.Models;

namespace Rheo.Storage.MIME
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
            byte[] headerBuffer = ReadFileHeader(filePath, SCAN_WINDOW_SIZE);
            
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

            // Calculate confidence percentages
            foreach (var result in results)
            {
                result.Confidence = totalPoints > 0 ? (result.Points * 100.0) / totalPoints : 0;
            }

            return [.. results.OrderByDescending(r => r.Points)];
        }

        private static byte[] ReadFileHeader(string filePath, int maxSize)
        {
            using var fileStream = File.OpenRead(filePath);
            int size = (int)Math.Min(fileStream.Length, maxSize);
            byte[] buffer = new byte[size];
            fileStream.ReadExactly(buffer, 0, size);
            return buffer;
        }

        /// <summary>
        /// Identifies and returns all definitions whose signature patterns match the provided file header buffer.
        /// </summary>
        /// <remarks>This method scans the header buffer for known pattern matches and validates each
        /// candidate definition by checking all required patterns. Only definitions with all patterns matching the
        /// buffer are included in the result. The method does not modify the input buffer.</remarks>
        /// <param name="headerBuffer">A byte array containing the file header data to be scanned for matching signature patterns. Only the first
        /// 512 bytes are considered.</param>
        /// <returns>A set of definitions that match all required signature patterns within the specified header buffer. The set
        /// will be empty if no definitions match.</returns>
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
            int scanLimit = Math.Min(headerBuffer.Length, 512);
            for (int pos = 0; pos < scanLimit; pos++)
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

        /// <summary>
        /// Determines whether a specified byte pattern matches the contents of a buffer at a given offset.
        /// </summary>
        /// <remarks>If the pattern extends beyond the end of the buffer when starting at the specified
        /// offset, the method returns false. This method does not modify the buffer or pattern arrays.</remarks>
        /// <param name="buffer">The byte array to search for the pattern within. Must not be null.</param>
        /// <param name="offset">The zero-based index in the buffer at which to begin matching the pattern. Must be non-negative and less
        /// than or equal to buffer.Length - pattern.Length.</param>
        /// <param name="pattern">The byte array representing the pattern to match. Must not be null.</param>
        /// <returns>true if the pattern matches the buffer at the specified offset; otherwise, false.</returns>
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
                    else
                    {
                        // String not found - invalidate this definition
                        return 0;
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