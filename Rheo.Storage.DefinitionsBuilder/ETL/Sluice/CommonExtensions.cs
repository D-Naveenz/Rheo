using System.Collections.Frozen;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Sluice
{
    internal static partial class CommonExtensions
    {
        // Individual level sets for granular access
        public static FrozenSet<string> Level1 => Level1Set.Value;
        public static FrozenSet<string> Level2 => Level2Set.Value;
        public static FrozenSet<string> Level3 => Level3Set.Value;
        public static FrozenSet<string> Level4 => Level4Set.Value;
        public static FrozenSet<string> Level5 => Level5Set.Value;

        // Combined sets for different scenarios
        public static FrozenSet<string> HighPriority => HighPrioritySet.Value;
        public static FrozenSet<string> MediumPriority => MediumPrioritySet.Value;
        public static FrozenSet<string> LowPriority => LowPrioritySet.Value;
        public static FrozenSet<string> AllCommon => AllCommonSet.Value;

        // Helper methods to build each level's set
        private static readonly Lazy<FrozenSet<string>> Level1Set = new(() =>
            BuildLevel1().ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> Level2Set = new(() =>
            BuildLevel2().ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> Level3Set = new(() =>
            BuildLevel3().ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> Level4Set = new(() =>
            BuildLevel4().ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> Level5Set = new(() =>
            BuildLevel5().ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        // Combined sets for different priority levels
        private static readonly Lazy<FrozenSet<string>> HighPrioritySet = new(() =>
            Level1.Union(Level2).ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> MediumPrioritySet = new(() =>
            Level3.Union(Level4).ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> LowPrioritySet = new(() =>
            Level5.ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        private static readonly Lazy<FrozenSet<string>> AllCommonSet = new(() =>
            HighPriority.Union(MediumPriority).Union(LowPriority)
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Determines the level associated with the specified file extension.
        /// </summary>
        /// <remarks>The method normalizes the input extension by removing a leading period and converting
        /// it to lowercase before evaluation. If <paramref name="extension"/> is <see langword="null"/> or empty, the
        /// method returns 0.</remarks>
        /// <param name="extension">The file extension to evaluate. May include or omit the leading period ('.') character. Case-insensitive.</param>
        /// <returns>An integer representing the level associated with the extension. Returns a value from 1 to 5 if the
        /// extension is recognized; otherwise, returns 0.</returns>
        public static int GetLevel(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return 0;

            var normalized = extension.StartsWith('.')
                ? extension[1..]
                : extension;

            if (Level1.Contains(normalized)) return 1;
            if (Level2.Contains(normalized)) return 2;
            if (Level3.Contains(normalized)) return 3;
            if (Level4.Contains(normalized)) return 4;
            if (Level5.Contains(normalized)) return 5;

            return 0;
        }

        /// <summary>
        /// Determines the level value based on an array of file extensions.
        /// </summary>
        /// <param name="extensions">An array of file extension strings to evaluate. Cannot be null; may be empty.</param>
        /// <returns>The level value associated with the provided extensions. Returns 0 if the array is null or empty.</returns>
        public static int GetLevel(string[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
                return 0;
            int highestLevel = 0;
            foreach (var ext in extensions)
            {
                var level = GetLevel(ext);
                if (highestLevel > 0)
                {
                    if (highestLevel > level)
                        highestLevel = level;
                    continue;
                }
                else
                {
                    highestLevel = level;
                }
            }
            return highestLevel;
        }

        /// <summary>
        /// Determines whether the specified file extension is a commonly used extension.
        /// </summary>
        /// <remarks>The comparison is case-insensitive. An empty or null value returns <see
        /// langword="false"/>.</remarks>
        /// <param name="extension">The file extension to check, with or without a leading period (for example, ".txt" or "txt").</param>
        /// <returns><see langword="true"/> if the extension is recognized as common; otherwise, <see langword="false"/>.</returns>
        public static bool IsCommonExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            var normalized = extension.StartsWith('.')
                ? extension[1..]
                : extension;

            return AllCommon.Contains(normalized);
        }

        /// <summary>
        /// Returns a set of file extensions associated with the specified inclusive range of levels.
        /// </summary>
        /// <param name="minLevel">The minimum level, inclusive, for which to retrieve file extensions. Must be between 1 and 5.</param>
        /// <param name="maxLevel">The maximum level, inclusive, for which to retrieve file extensions. Must be between <paramref
        /// name="minLevel"/> and 5. The default is 5.</param>
        /// <returns>A frozen set containing all file extensions associated with levels in the specified range. The set is
        /// case-insensitive and will be empty if no extensions are found for the range.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="minLevel"/> is less than 1 or greater than 5, or if <paramref name="maxLevel"/> is
        /// less than <paramref name="minLevel"/> or greater than 5.</exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static FrozenSet<string> GetExtensionsByLevelRange(int minLevel, int maxLevel = 5)
        {
            if (minLevel < 1 || minLevel > 5 || maxLevel < minLevel || maxLevel > 5)
                throw new ArgumentException("Invalid level range");

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int level = minLevel; level <= maxLevel; level++)
            {
                var set = level switch
                {
                    1 => Level1,
                    2 => Level2,
                    3 => Level3,
                    4 => Level4,
                    5 => Level5,
                    _ => throw new InvalidOperationException()
                };

                result.UnionWith(set);
            }

            return result.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
