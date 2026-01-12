using Rheo.Storage.DefinitionsBuilder.ETL.RIFF;

namespace Rheo.Storage.DefinitionsBuilder.Analyze
{
    public static class TridFileAnalyzer
    {
        private const int HEADER_FRONT_SIZE = 2048;
        private const int MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Analyzes the specified file to identify its type based on predefined patterns and strings.
        /// </summary>
        /// <remarks>This method analyzes the file by comparing its content against a set of predefined
        /// patterns and strings provided in the <paramref name="definitionsBlock"/>. The analysis includes the
        /// following steps: <list type="bullet"> <item>Matching patterns in the file's front block.</item>
        /// <item>Optionally checking for specific strings in the file's content if <paramref name="checkStrings"/> is
        /// <see langword="true"/>.</item> </list> The results are scored based on the number and quality of matches,
        /// and the scores are normalized to percentages.</remarks>
        /// <param name="filePath">The path to the file to be analyzed. The file must exist and cannot be empty.</param>
        /// <param name="definitionsBlock">A collection of file type definitions used to match patterns and strings in the file.</param>
        /// <param name="checkStrings">A boolean value indicating whether to perform string-based checks in addition to pattern matching. If <see
        /// langword="true"/>, string checks are performed; otherwise, only pattern matching is used.</param>
        /// <returns>A list of <see cref="TrIDResult"/> objects representing the matching file type definitions, including their
        /// respective scores and percentages. Returns an empty list if no matches are found.</returns>
        public static List<TrIDResult> AnalyzeFile(string filePath, Dictionary<int, List<TrIDDefinition>> definitionsBlock, bool checkStrings = true)
        {
            var results = new List<TrIDResult>();
            var foundCache = new HashSet<string>();
            var stopCache = new HashSet<string>();
            var totalPoints = 0;

            if (!File.Exists(filePath))
                return results;

            long fileSize = new FileInfo(filePath).Length;
            if (fileSize == 0)
                return results;

            // Read the front part of the file
            byte[] frontBlock;
            using (var fileStream = File.OpenRead(filePath))
            {
                int frontSize = (int)Math.Min(fileSize, HEADER_FRONT_SIZE);
                frontBlock = new byte[frontSize];
                fileStream.ReadExactly(frontBlock, 0, frontSize);
            }

            // Get definitions based on first byte
            var definitionsToCheck = new List<TrIDDefinition>();
            definitionsToCheck.AddRange(definitionsBlock[-1]);

            if (frontBlock.Length > 0)
            {
                definitionsToCheck.AddRange(definitionsBlock[frontBlock[0]]);
            }

            byte[]? fileBuffer = null;

            foreach (var definition in definitionsToCheck)
            {
                int points = 0;

                // Check patterns
                foreach (var pattern in definition.Patterns)
                {
                    if (frontBlock.Length >= pattern.Position + pattern.Data.Length)
                    {
                        bool match = true;
                        for (int i = 0; i < pattern.Data.Length; i++)
                        {
                            if (frontBlock[pattern.Position + i] != pattern.Data[i])
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            if (pattern.Position == 0)
                                points += pattern.Data.Length * 1000;
                            else
                                points += pattern.Data.Length;
                        }
                        else
                        {
                            points = 0;
                            break;
                        }
                    }
                    else
                    {
                        points = 0;
                        break;
                    }
                }

                // Check strings if patterns matched
                if (points > 0 && checkStrings && definition.Strings.Count > 0)
                {
                    fileBuffer ??= LoadDataFromFile(filePath);

                    bool skipSearch = false;

                    // Check if any string is known to be a show-stopper
                    foreach (var stringBytes in definition.Strings)
                    {
                        string stringKey = Convert.ToBase64String(stringBytes);
                        if (stopCache.Contains(stringKey))
                        {
                            skipSearch = true;
                            points = 0;
                            break;
                        }
                    }

                    if (!skipSearch)
                    {
                        foreach (var stringBytes in definition.Strings)
                        {
                            string stringKey = Convert.ToBase64String(stringBytes);

                            if (foundCache.Contains(stringKey))
                            {
                                points += stringBytes.Length * 500;
                            }
                            else
                            {
                                // Search for the string in the file buffer
                                if (ContainsSequence(fileBuffer, stringBytes))
                                {
                                    points += stringBytes.Length * 500;
                                    foundCache.Add(stringKey);
                                }
                                else
                                {
                                    points = 0;
                                    stopCache.Add(stringKey);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Add result if points > 0
                if (points > 0)
                {
                    totalPoints += points;
                    var result = new TrIDResult
                    {
                        Points = points,
                        PatternCount = definition.Patterns.Count,
                        StringCount = definition.Strings.Count,
                        Definition = definition
                    };
                    results.Add(result);
                }
            }

            // Calculate percentages
            foreach (var result in results)
            {
                result.Percentage = result.Points * 100.0 / totalPoints;
            }

            // Sort by points (descending)
            return [.. results.OrderByDescending(r => r.Points)];
        }

        private static byte[] LoadDataFromFile(string filePath)
        {
            long fileSize = new FileInfo(filePath).Length;

            if (fileSize <= MAX_FILE_SIZE)
            {
                return File.ReadAllBytes(filePath);
            }
            else
            {
                int partSize = MAX_FILE_SIZE / 2;
                using var fileStream = File.OpenRead(filePath);

                // Read first part
                byte[] data = new byte[MAX_FILE_SIZE + 1]; // +1 for separator
                fileStream.ReadExactly(data, 0, partSize);

                // Add separator
                data[partSize] = (byte)'|';

                // Read last part
                fileStream.Seek(fileSize - partSize, SeekOrigin.Begin);
                fileStream.ReadExactly(data, partSize + 1, partSize);

                return data;
            }
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
