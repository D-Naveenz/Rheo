using Rheo.Storage.FileDefinition.Models;
using Spectre.Console;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Validation
{
    public static class ValidationExtentions
    {
        private readonly static Dictionary<string, List<Definition>> _invalidGroupedDefinitions = [];

        /// <summary>
        /// Validates a collection of <see cref="Definition"/> objects and returns the results grouped by MIME type.
        /// </summary>
        /// <param name="definitions">The collection of <see cref="Definition"/> objects to validate. Cannot be null.</param>
        /// <param name="report"><see langword="true"/> to include detailed validation reports in the results; otherwise, <see
        /// langword="false"/> to return only validation outcomes.</param>
        /// <returns>A tuple containing two dictionaries, each mapping MIME type strings to lists of <see cref="Definition"/>
        /// objects: the first dictionary contains valid definitions, and the second contains invalid definitions, both
        /// grouped by MIME type.</returns>
        public static (Dictionary<string, List<Definition>>, Dictionary<string, List<Definition>>) Validate(
            this IEnumerable<Definition> definitions, 
            bool report = false
        )
        {
            var groupedDefinitions = definitions.GroupByMimeType();
            return report ? ValidateAndReportInternal(groupedDefinitions) : ValidateInternal(groupedDefinitions);
        }

        /// <summary>
        /// Validates and normalizes MIME type groupings, separating valid and invalid definitions.
        /// </summary>
        /// <remarks>This method processes each group of definitions by attempting to clean and validate
        /// the MIME type key. Definitions with valid, recognized MIME types are grouped under their normalized form,
        /// and their <c>MimeType</c> property is updated accordingly. Definitions with invalid or unrecognized MIME
        /// types are grouped separately under their original key.</remarks>
        /// <param name="definitions">A dictionary mapping MIME type strings to lists of <see cref="Definition"/> objects. Each key represents an
        /// original MIME type, and each value is the associated list of definitions.</param>
        /// <returns>A tuple containing two dictionaries: <list type="bullet">   <item>     <description>The first dictionary
        /// maps cleaned, valid MIME types to lists of <see cref="Definition"/> objects with updated <c>MimeType</c>
        /// properties.</description>   </item>   <item>     <description>The second dictionary maps invalid or
        /// unrecognized MIME type strings to their corresponding lists of <see cref="Definition"/>
        /// objects.</description>   </item> </list></returns>
        private static (Dictionary<string, List<Definition>>, Dictionary<string, List<Definition>>) ValidateInternal(
            Dictionary<string, List<Definition>> definitions
        )
        {
            var validDefsGrouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            var invalidDefsGrouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            var cleaner = new MimeTypeCleaner(MimeTypes.Load());

            // Process each MIME type group
            foreach (var kvp in definitions)
            {
                string originalMime = kvp.Key;
                var definitionList = kvp.Value;

                var cleanedMime = cleaner.CleanMimeType(originalMime);

                if (cleanedMime == null)
                {
                    // Handle invalid MIME types separately
                    invalidDefsGrouped.TryAdd(originalMime, []);
                    invalidDefsGrouped[originalMime].AddRange(definitionList);
                }
                else
                {
                    // Add the updated definitions with cleaned MIME type to the dictionary 
                    validDefsGrouped.TryAdd(cleanedMime, []);

                    // Update each definition with the cleaned MIME type
                    foreach (var definition in definitionList)
                    {
                        definition.MimeType = cleanedMime;
                        validDefsGrouped[cleanedMime].Add(definition);
                    }
                }
            }

            return (validDefsGrouped, invalidDefsGrouped);
        }

        /// <summary>
        /// Validates and cleans MIME type definitions, grouping them into valid and invalid categories.
        /// </summary>
        /// <remarks>This method processes each group of definitions by MIME type, attempts to clean and
        /// validate the MIME type, and updates the <see cref="Definition.MimeType"/> property for valid entries.
        /// Invalid MIME types are grouped separately. Progress is reported to the console during processing.</remarks>
        /// <param name="definitions">A dictionary mapping original MIME type strings to lists of <see cref="Definition"/> objects to be validated
        /// and cleaned.</param>
        /// <returns>A tuple containing two dictionaries: <list type="bullet"> <item> <description> The first dictionary maps
        /// cleaned, valid MIME type strings to lists of <see cref="Definition"/> objects with updated MIME types.
        /// </description> </item> <item> <description> The second dictionary maps invalid or unrecognized MIME type
        /// strings to their corresponding lists of <see cref="Definition"/> objects. </description> </item> </list></returns>
        private static (Dictionary<string, List<Definition>>, Dictionary<string, List<Definition>>) ValidateAndReportInternal(
            Dictionary<string, List<Definition>> definitions
        )
        {
            var validDefsGrouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            var invalidDefsGrouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
            var cleaner = new MimeTypeCleaner(MimeTypes.Load());

            // Calculate total definitions for progress reporting
            var totalDefinitions = definitions.Values.Sum(list => list.Count);
            var processedDefinitions = 0;
            var validDefinitions = 0;
            var invalidDefinitions = 0;

            // Process each MIME type group
            var mimeTypeCount = definitions.Count;
            var currentMimeIndex = 0;

            AnsiConsole.Progress()
                .AutoClear(true)
                .Start(ctx =>
                {
                    // Define tasks
                    var task1 = ctx.AddTask("[green]Validating Mime Types:[/]");
                    var task2 = ctx.AddTask("[green]Processing Definitions:[/]");

                    foreach (var kvp in definitions)
                    {
                        currentMimeIndex++;
                        string originalMime = kvp.Key;
                        var definitionList = kvp.Value;
                        int groupSize = definitionList.Count;

                        var cleanedMime = cleaner.CleanMimeType(originalMime);

                        if (cleanedMime == null)
                        {
                            // Handle invalid MIME types separately
                            invalidDefsGrouped.TryAdd(originalMime, []);
                            invalidDefsGrouped[originalMime].AddRange(definitionList);
                            invalidDefinitions += groupSize;
                        }
                        else
                        {
                            // Add the updated definitions with cleaned MIME type to the dictionary 
                            validDefsGrouped.TryAdd(cleanedMime, []);

                            // Update each definition with the cleaned MIME type
                            foreach (var definition in definitionList)
                            {
                                definition.MimeType = cleanedMime;
                                validDefsGrouped[cleanedMime].Add(definition);
                            }

                            validDefinitions += groupSize;
                        }

                        processedDefinitions += groupSize;

                        // Report current MIME type being processed
                        task1.Value = (double)currentMimeIndex / mimeTypeCount * 100;
                        task2.Value = (double)processedDefinitions / totalDefinitions * 100;
                    }
                });

            return (validDefsGrouped, invalidDefsGrouped);
        }
    }
}
