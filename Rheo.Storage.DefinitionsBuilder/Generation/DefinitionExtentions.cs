using Rheo.Storage.DefinitionsBuilder.Models.Definition;

namespace Rheo.Storage.DefinitionsBuilder.Generation
{
    internal static class DefinitionExtentions
    {
        private readonly static Dictionary<string, List<Definition>> _invalidGroupedDefinitions = [];

        /// <summary>
        /// Groups a list of <see cref="Definition"/> objects by their MIME type.
        /// </summary>
        /// <remarks>Definitions with a null, empty, or whitespace-only MIME type are excluded from the
        /// returned dictionary.</remarks>
        /// <param name="definitions">The list of <see cref="Definition"/> objects to group. Each object should have a valid MIME type.</param>
        /// <returns>A dictionary where the keys are MIME types (case-insensitive) and the values are lists of <see
        /// cref="Definition"/> objects associated with each MIME type.</returns>
        public static Dictionary<string, List<Definition>> GroupByMimeType(this List<Definition> definitions)
        {
            var groupedDefinitions = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.MimeType))
                {
                    _invalidGroupedDefinitions.TryAdd(definition.MimeType, []);
                    _invalidGroupedDefinitions[definition.MimeType].Add(definition);
                    continue;
                }

                groupedDefinitions.TryAdd(definition.MimeType, []);
                groupedDefinitions[definition.MimeType].Add(definition);
            }
            return groupedDefinitions;
        }

        /// <summary>
        /// Cleanses the MIME type keys in the provided dictionary and groups the associated definitions by their
        /// cleaned MIME types.
        /// </summary>
        /// <remarks>This method processes the MIME type keys in a case-insensitive manner. Invalid MIME
        /// types are excluded from the returned dictionary and handled separately. The <see
        /// cref="Definition.MimeType"/> property of each definition is updated to reflect the cleaned MIME
        /// type.</remarks>
        /// <param name="definitions">A dictionary where the keys represent MIME types and the values are lists of <see cref="Definition"/>
        /// objects associated with those MIME types.</param>
        /// <returns>A new dictionary where the keys are the cleaned MIME types and the values are lists of <see
        /// cref="Definition"/> objects updated with the cleaned MIME types.</returns>
        public static Dictionary<string, List<Definition>> Cleanse(this Dictionary<string, List<Definition>> definitions)
        {
            var grouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            var cleaner = new MimeTypeCleaner(MimeTypes.Load());

            foreach (var mime in definitions.Keys)
            {
                var cleanedMime = cleaner.CleanMimeType(mime);

                if (cleanedMime == null)
                {
                    // Handle invalid MIME types separately
                    _invalidGroupedDefinitions.TryAdd(mime, []);
                    _invalidGroupedDefinitions[mime].AddRange(definitions[mime]);
                    continue;
                }

                // Add the updated definitions with cleaned MIME type to the dictionary 
                grouped.TryAdd(cleanedMime, []);
                grouped[cleanedMime].AddRange([.. definitions[mime]
                    .Select(d => {
                        d.MimeType = cleanedMime; return d;
                    })]);
            }

            return grouped;
        }
    }
}
