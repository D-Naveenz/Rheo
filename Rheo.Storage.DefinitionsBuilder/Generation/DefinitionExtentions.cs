using Rheo.Storage.DefinitionsBuilder.Models.Build;
using Rheo.Storage.DefinitionsBuilder.Models.Definition;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// Removes invalid or duplicate <see cref="Definition"/> entries from the specified dictionary.
        /// </summary>
        /// <param name="definitions">A dictionary containing lists of <see cref="Definition"/> objects to be cleansed. The dictionary keys
        /// represent categories or identifiers, and the values are the corresponding lists of definitions to process.</param>
        /// <param name="report"><see langword="true"/> to generate a report of the cleansing process; otherwise, <see langword="false"/> to
        /// perform cleansing without reporting. The report may include details about removed or modified entries.</param>
        /// <returns>A new dictionary containing the cleansed lists of <see cref="Definition"/> objects. The structure and keys
        /// of the dictionary are preserved, but invalid or duplicate definitions are removed from the lists.</returns>
        public static Dictionary<string, List<Definition>> Cleanse(this Dictionary<string, List<Definition>> definitions, bool report = false)
        {
            return report ? CleanseAndReportInternal(definitions) : CleanseInternal(definitions);
        }

        public static void CreateMemoryDump(this Dictionary<string, List<Definition>> definitions, string? dumpPath = null)
        {
            var validDefCount = 0;
            var invalidDefCount = 0;

            // Process valid definitions
            var validMimeTypes = new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in definitions)
            {
                validMimeTypes[def.Key] = [.. def.Value.Select(d => d.Extension).Distinct(StringComparer.OrdinalIgnoreCase)];
                validDefCount += def.Value.Count;
            }
            var validMemoryDump = new MemoryDump
            {
                DefinitionsCount = validDefCount,
                MimeCount = validMimeTypes.Keys.Count,
                MimeTypes = validMimeTypes
            };

            // Process invalid definitions
            var invalidMimeTypes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in _invalidGroupedDefinitions)
            {
                invalidMimeTypes[def.Key ?? "null"] = [.. def.Value.Select(d => d.Extension).Distinct(StringComparer.OrdinalIgnoreCase)];
                invalidDefCount += def.Value.Count;
            }
            var invalidMemoryDump = new MemoryDump
            {
                DefinitionsCount = invalidDefCount,
                MimeCount = invalidMimeTypes.Keys.Count,
                MimeTypes = invalidMimeTypes
            };

            // Export memory dumps
            ExportMemoryDump(validMemoryDump, "ValidMimeTypes.dump.json", dumpPath);
            ExportMemoryDump(invalidMemoryDump, "InvalidMimeTypes.dump.json", dumpPath);
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
        private static Dictionary<string, List<Definition>> CleanseInternal(Dictionary<string, List<Definition>> definitions)
        {
            var grouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
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
                    _invalidGroupedDefinitions.TryAdd(originalMime, []);
                    _invalidGroupedDefinitions[originalMime].AddRange(definitionList);
                }
                else
                {
                    // Add the updated definitions with cleaned MIME type to the dictionary 
                    grouped.TryAdd(cleanedMime, []);

                    // Update each definition with the cleaned MIME type
                    foreach (var definition in definitionList)
                    {
                        definition.MimeType = cleanedMime;
                        grouped[cleanedMime].Add(definition);
                    }
                }
            }

            return grouped;
        }

        /// <summary>
        /// Cleanses MIME type keys in the provided dictionary and returns a new dictionary grouped by the cleaned MIME
        /// types.
        /// </summary>
        /// <remarks>This method processes each MIME type in the input dictionary, attempts to cleanse it,
        /// and updates the <c>MimeType</c> property of each associated <see cref="Definition"/>. Invalid MIME types are
        /// excluded from the returned dictionary. Progress is reported to the console during execution.</remarks>
        /// <param name="definitions">A dictionary where each key is a MIME type string and each value is a list of <see cref="Definition"/>
        /// objects associated with that MIME type.</param>
        /// <returns>A new dictionary containing the cleaned MIME type strings as keys and lists of <see cref="Definition"/>
        /// objects with updated <c>MimeType</c> properties as values. Only valid, cleansed MIME types are included in
        /// the returned dictionary.</returns>
        private static Dictionary<string, List<Definition>> CleanseAndReportInternal(Dictionary<string, List<Definition>> definitions)
        {
            var grouped = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);
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
                .Start(ctx =>
                {
                    // Define tasks
                    var task1 = ctx.AddTask("[green]Cleansed Mime Types:[/] (0 valid, 0 invalid)");
                    var task2 = ctx.AddTask("[green]Cleansed Definitions:[/] (0/0)");

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
                            _invalidGroupedDefinitions.TryAdd(originalMime, []);
                            _invalidGroupedDefinitions[originalMime].AddRange(definitionList);
                            invalidDefinitions += groupSize;
                        }
                        else
                        {
                            // Add the updated definitions with cleaned MIME type to the dictionary 
                            grouped.TryAdd(cleanedMime, []);

                            // Update each definition with the cleaned MIME type
                            foreach (var definition in definitionList)
                            {
                                definition.MimeType = cleanedMime;
                                grouped[cleanedMime].Add(definition);
                            }

                            validDefinitions += groupSize;
                        }

                        processedDefinitions += groupSize;

                        // Report current MIME type being processed
                        task1.Value = (double)currentMimeIndex / mimeTypeCount * 100;
                        task1.Description = $"[green]Cleansed Mime Types:[/] ({validDefinitions} valid, {invalidDefinitions} invalid)";
                        task2.Value = (double)processedDefinitions / totalDefinitions * 100;
                        task2.Description = $"[green]Cleansed Definitions:[/] ({processedDefinitions}/{totalDefinitions})";
                    }
                });

            return grouped;
        }

        private static void ExportMemoryDump(MemoryDump memoryDump, string fileName,string? outputPath = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = Path.Combine(Directory.GetCurrentDirectory(), "MemoryDumps");
                }

                outputPath = Path.GetFullPath(outputPath!);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                var filePath = Path.Combine(outputPath, fileName);
                var json = JsonSerializer.Serialize(memoryDump, MemoryDumpJsonContext.Default.MemoryDump);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export memory dump: {ex.Message}");
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(MemoryDump))]
    internal partial class MemoryDumpJsonContext : JsonSerializerContext
    {
    }
}
