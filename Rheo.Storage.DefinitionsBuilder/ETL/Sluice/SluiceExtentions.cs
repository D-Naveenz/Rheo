using Rheo.Storage.MIME.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Sluice
{
    public static class SluiceExtentions
    {
        /// <summary>
        /// Filters a collection of <see cref="Definition"/> objects to include only those whose extension levels fall
        /// within the specified range.
        /// </summary>
        /// <param name="definitions">The collection of <see cref="Definition"/> objects to filter. Cannot be <see langword="null"/>.</param>
        /// <param name="minLevel">The minimum extension level to include, inclusive. Must be between 1 and 5.</param>
        /// <param name="maxLevel">The maximum extension level to include, inclusive. Must be between <paramref name="minLevel"/> and 5.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the <see cref="Definition"/> objects whose extension levels are
        /// within the specified range.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="minLevel"/> or <paramref name="maxLevel"/> is outside the range 1 to 5, or if
        /// <paramref name="maxLevel"/> is less than <paramref name="minLevel"/>.</exception>
        public static IEnumerable<Definition> FilterByExtensionLevels(
            this IEnumerable<Definition> definitions,
            int minLevel = 1,
            int maxLevel = 5)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            if (minLevel < 1 || minLevel > 5 || maxLevel < minLevel || maxLevel > 5)
                throw new ArgumentException("Invalid level range");

            var allowedExtensions = CommonExtensions.GetExtensionsByLevelRange(minLevel, maxLevel);

            return definitions.Where(d =>
                d.Extensions.Length > 0 &&
                d.Extensions.Any(
                    ext => !string.IsNullOrWhiteSpace(ext) && allowedExtensions.Contains(NormalizeExtension(ext))
                    )
                );
        }

        
        /// <summary>
        /// Groups a collection of <see cref="Definition"/> objects by their extension level.
        /// </summary>
        /// <remarks>The extension level for each <see cref="Definition"/> is determined by its
        /// <c>Extension</c> property using <c>CommonExtensions.GetLevel</c>. Definitions with a null or empty
        /// <c>Extension</c> are grouped under key 0.</remarks>
        /// <param name="definitions">The collection of <see cref="Definition"/> objects to group. Cannot be null.</param>
        /// <returns>A dictionary where each key is an extension level (1 through 5, or 0 for uncommon or missing extensions),
        /// and the value is a list of <see cref="Definition"/> objects assigned to that level. </returns>
        public static Dictionary<int, List<Definition>> GroupByExtensionLevel(this IEnumerable<Definition> definitions)
        {
            var result = new Dictionary<int, List<Definition>>
            {
                [1] = [],
                [2] = [],
                [3] = [],
                [4] = [],
                [5] = [],
                [0] = [] // Uncommon extensions
            };

            foreach (var definition in definitions)
            {
                if (definition.Extensions == null || definition.Extensions.Length == 0)
                {
                    result[0].Add(definition);
                    continue;
                }

                int level;
                if (definition.Extensions.Length == 1)
                {
                    level = CommonExtensions.GetLevel(definition.Extensions[0]);
                }
                else
                {
                    level = CommonExtensions.GetLevel(definition.Extensions);
                }

                definition.PriorityLevel = Valuation.CalculatePriority(definition, level);
                result[level].Add(definition);
            }

            return result;
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return string.Empty;

            var ext = extension.StartsWith('.')
                ? extension[1..]
                : extension;

            return ext.ToLowerInvariant();
        }
    }
}
