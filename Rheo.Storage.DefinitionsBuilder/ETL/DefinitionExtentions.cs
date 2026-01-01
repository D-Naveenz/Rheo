using Rheo.Storage.FileDefinition.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL
{
    public static class DefinitionExtentions
    {
        /// <summary>
        /// Groups a collection of <see cref="Definition"/> objects by their MIME type.
        /// </summary>
        /// <remarks>The comparison of MIME types is case-insensitive. If the input collection is empty,
        /// the returned dictionary will also be empty.</remarks>
        /// <param name="definitions">The collection of <see cref="Definition"/> objects to group. Cannot be <see langword="null"/>.</param>
        /// <returns>A dictionary where each key is a MIME type, and the corresponding value is a list of <see
        /// cref="Definition"/> objects with that MIME type.</returns>
        public static Dictionary<string, List<Definition>> GroupByMimeType(this IEnumerable<Definition> definitions)
        {
            var groupedDefinitions = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
            {
                groupedDefinitions.TryAdd(definition.MimeType, []);
                groupedDefinitions[definition.MimeType].Add(definition);
            }
            return groupedDefinitions;
        }

        /// <summary>
        /// Flattens a dictionary of definitions into a single list of definitions.
        /// </summary>
        /// <remarks>This method combines all the lists of definitions from the dictionary values into a
        /// single list. The order of elements in the resulting list corresponds to the order of the dictionary's values
        /// and the order of items within each value.</remarks>
        /// <param name="definitions">A dictionary where the key is an integer and the value is a list of <see cref="Definition"/> objects.</param>
        /// <returns>A list containing all <see cref="Definition"/> objects from the input dictionary, preserving their original
        /// order within each list.</returns>
        public static List<Definition> Flatten(this Dictionary<string, List<Definition>> definitions)
        {
            return [.. definitions.SelectMany(kvp => kvp.Value)];
        }
    }
}
