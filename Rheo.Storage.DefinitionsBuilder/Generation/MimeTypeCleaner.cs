namespace Rheo.Storage.DefinitionsBuilder.Generation
{
    public class MimeTypeCleaner(HashSet<string> validMimeTypes)
    {
        private readonly HashSet<string> _validMimeTypes = validMimeTypes ?? throw new ArgumentNullException(nameof(validMimeTypes));
        private readonly HashSet<string> _validMimeTypesLower = [.. validMimeTypes.Select(m => m.ToLowerInvariant())];

        /// <summary>
        /// Cleans a MIME type string by removing common impurities and correcting case
        /// </summary>
        public string? CleanMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                throw new ArgumentException("MIME type cannot be null or whitespace.", nameof(mimeType));

            // Step 1: Basic cleaning
            string cleaned = CleanBasic(mimeType);

            // Step 2: Try direct case-insensitive match
            if (TryDirectMatch(cleaned, out string? matched))
                return matched;

            // Step 4: Try fuzzy matching
            if (TryFuzzyMatch(cleaned, out matched))
                return matched;

            return null; // No match found
        }

        #region Cleaning Strategies

        /// <summary>
        /// Cleans the specified MIME type string by removing extra whitespace, correcting common errors, and converting
        /// it to lowercase.
        /// </summary>
        /// <param name="mimeType">The MIME type string to be cleaned. Cannot be <see langword="null"/> or empty.</param>
        /// <returns>A cleaned and normalized MIME type string in lowercase.</returns>
        private static string CleanBasic(string mimeType)
        {
            // Remove extra whitespace
            string cleaned = mimeType.Trim();

            // Remove common prefixes/suffixes that might be errors
            cleaned = RemoveCommonErrors(cleaned);

            // Convert to lowercase for comparison
            return cleaned.ToLowerInvariant();
        }

        /// <summary>
        /// Removes common errors and misspellings from the specified MIME type string.
        /// </summary>
        /// <remarks>This method addresses common misspellings and formatting issues in MIME type strings,
        /// such as incorrect prefixes or trailing special characters. For example, it corrects "applicaiton/json" to
        /// "application/json" and trims characters like ';', ',', '.', or '"' from the beginning or end of the
        /// string.</remarks>
        /// <param name="mimeType">The MIME type string to process. This value is case-insensitive.</param>
        /// <returns>A corrected MIME type string with common errors fixed and leading or trailing special characters removed.</returns>
        private static string RemoveCommonErrors(string mimeType)
        {
            // Common misspellings and errors
            var commonErrors = new Dictionary<string, string>
            {
                { "aapplication", "application" },
                { "applicaiton", "application" },
                { "appliction", "application" },
                { "apllication", "application" },
                { "image/", "image/" }, // Remove duplicate slashes later
                { "text/", "text/" },
                { "video/", "video/" },
                { "audio/", "audio/" },
                { "chemical/", "chemical/" },
                { "message/", "message/" },
                { "model/", "model/" },
                { "multipart/", "multipart/" }
            };

            string result = mimeType;

            // Fix common prefix errors
            foreach (var error in commonErrors)
            {
                if (result.StartsWith(error.Key, StringComparison.OrdinalIgnoreCase))
                {
                    result = string.Concat(error.Value, result.AsSpan(error.Key.Length));
                    break;
                }
            }

            // Remove trailing/leading special characters
            result = result.Trim(';', ',', '.', '"');

            return result;
        }

        /// <summary>
        /// Attempts to find a direct match for the specified MIME type in the valid MIME types collection.
        /// </summary>
        /// <param name="mimeType">The MIME type to search for. The comparison is case-insensitive.</param>
        /// <param name="matched">When this method returns, contains the correctly cased version of the matched MIME type if a match is found;
        /// otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/>.</returns>
        private bool TryDirectMatch(string mimeType, out string? matched)
        {
            // Direct case-insensitive match
            if (_validMimeTypesLower.Contains(mimeType))
            {
                // Find the correctly cased version from the original set
                matched = _validMimeTypes.First(m =>
                    string.Equals(m, mimeType, StringComparison.OrdinalIgnoreCase));
                return true;
            }

            matched = null;
            return false;
        }

        /// <summary>
        /// Attempts to find a valid MIME type that closely matches the specified MIME type using a fuzzy matching
        /// algorithm.
        /// </summary>
        /// <remarks>This method uses a similarity threshold of 70% to determine whether a valid MIME type
        /// is a close match. The input MIME type must be in the format "type/subtype". If no valid matches are found,
        /// the <paramref name="matched"/> parameter will be set to <see langword="null"/>.</remarks>
        /// <param name="mimeType">The MIME type to match, in the format "type/subtype".</param>
        /// <param name="matched">When this method returns, contains the closest matching valid MIME type if a match is found; otherwise, <see
        /// langword="null"/>.</param>
        /// <returns><see langword="true"/> if a matching MIME type is found; otherwise, <see langword="false"/>.</returns>
        private bool TryFuzzyMatch(string mimeType, out string? matched)
        {
            // Split into type and subtype
            if (mimeType.Contains('/'))
            {
                string[] parts = mimeType.Split('/', 2);
                string type = parts[0].Trim();
                string subtype = parts[1].Trim();

                // Try to find similar MIME types
                var candidates = _validMimeTypesLower
                    .Where(m => m.Contains('/'))
                    .Select(m => new
                    {
                        MimeType = m,
                        Parts = m.Split('/', 2),
                        Similarity = CalculateSimilarity(type, subtype, m)
                    })
                    .Where(x => x.Similarity > 0.7) // 70% similarity threshold
                    .OrderByDescending(x => x.Similarity)
                    .ToList();

                if (candidates.Count != 0)
                {
                    matched = _validMimeTypes.First(m =>
                        string.Equals(m, candidates[0].MimeType, StringComparison.OrdinalIgnoreCase));
                    return true;
                }
            }

            matched = null;
            return false;
        }

        /// <summary>
        /// Calculates the similarity score between a specified type and subtype combination and a candidate string.
        /// </summary>
        /// <remarks>The method splits the candidate string into its type and subtype components and
        /// calculates similarity scores for each. The final score is a weighted combination of the type similarity
        /// (30%) and the subtype similarity (70%). If the candidate string does not contain a '/' character, the method
        /// returns 0.</remarks>
        /// <param name="type">The primary type to compare, such as a media type or category.</param>
        /// <param name="subtype">The subtype to compare, typically a more specific classification within the type.</param>
        /// <param name="candidate">The candidate string to evaluate, expected to be in the format "type/subtype".</param>
        /// <returns>A similarity score as a floating-point value between 0 and 1, where 0 indicates no similarity and 1
        /// indicates an exact match. The subtype is weighted more heavily in the calculation.</returns>
        private static float CalculateSimilarity(string type, string subtype, string candidate)
        {
            if (!candidate.Contains('/'))
                return 0;

            string[] candidateParts = candidate.Split('/', 2);
            string candidateType = candidateParts[0];
            string candidateSubtype = candidateParts[1];

            // Calculate similarity scores
            float typeSimilarity = CalculateStringSimilarity(type, candidateType);
            float subtypeSimilarity = CalculateStringSimilarity(subtype, candidateSubtype);

            // Weight subtype more heavily
            return (typeSimilarity * 0.3f) + (subtypeSimilarity * 0.7f);
        }

        /// <summary>
        /// Calculates the similarity between two strings as a value between 0 and 1.
        /// </summary>
        /// <remarks>The similarity is calculated using a simplified algorithm based on the Levenshtein
        /// distance and the relative lengths of the strings. The result is normalized to a range of 0 to 1.</remarks>
        /// <param name="a">The first string to compare. Cannot be <see langword="null"/> or empty.</param>
        /// <param name="b">The second string to compare. Cannot be <see langword="null"/> or empty.</param>
        /// <returns>A value between 0 and 1 representing the similarity between the two strings, where 1 indicates identical
        /// strings (case-insensitive) and 0 indicates no similarity.</returns>
        private static float CalculateStringSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0;

            // Simple similarity calculation - can be replaced with more advanced algorithm
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
                return 1.0f;

            // Check for Levenshtein distance (simplified)
            int maxLength = Math.Max(a.Length, b.Length);
            int distance = LevenshteinDistance(a, b);

            return 1.0f - ((float)distance / maxLength);
        }

        /// <summary>
        /// Calculates the Levenshtein distance between two strings, which represents the minimum number of
        /// single-character edits (insertions, deletions, or substitutions) required to transform one string into the
        /// other.
        /// </summary>
        /// <remarks>This method uses a dynamic programming approach to compute the distance, with a time
        /// complexity of O(n * m), where n and m are the lengths of the input strings. It is case-sensitive and treats
        /// uppercase and lowercase letters as distinct.</remarks>
        /// <param name="a">The first string to compare. Can be null or empty.</param>
        /// <param name="b">The second string to compare. Can be null or empty.</param>
        /// <returns>The Levenshtein distance between the two strings. Returns 0 if both strings are null or empty. If one string
        /// is null or empty, returns the length of the other string.</returns>
        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return string.IsNullOrEmpty(b) ? 0 : b.Length;

            if (string.IsNullOrEmpty(b))
                return a.Length;

            int[,] distances = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                distances[i, 0] = i;

            for (int j = 0; j <= b.Length; j++)
                distances[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;

                    distances[i, j] = Math.Min(
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost);
                }
            }

            return distances[a.Length, b.Length];
        }

        #endregion
    }
}
