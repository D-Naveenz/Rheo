using MessagePack;
using Rheo.Storage.DefinitionsBuilder.Generation;
using Rheo.Storage.DefinitionsBuilder.Models.Definition;
using Rheo.Storage.DefinitionsBuilder.Models.Package;
using Rheo.Storage.DefinitionsBuilder.RIFF;
using Rheo.Storage.DefinitionsBuilder.RIFF.Models;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rheo.Storage.DefinitionsBuilder
{
    public class PackageCompiler
    {
        private readonly string _scriptPath;

        public Dictionary<int, List<TrIDDefinition>> Block { get; }

        public PackageCompiler()
        {
            // Verify paths
            var errormessage = "Couldn't find the TrID - File Identifier or the data package in the Data Folder.";
            _scriptPath = Path.GetFullPath(Path.Combine(Configuration.TridLocation, TrIDInfo.SCRIPT_NAME));
            if (!File.Exists(_scriptPath))
            {
                throw new FileNotFoundException(errormessage, TrIDInfo.SCRIPT_NAME);
            }
            var packagePath = Path.GetFullPath(Path.Combine(Configuration.TridLocation, TrIDInfo.PACKAGE_NAME));
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException(errormessage, TrIDInfo.PACKAGE_NAME);
            }

            // Parse TrID package
            Block = TridPackageParser.ParsePackage(packagePath);
        }

        public void Compile(string? outputPath = null)
        {
            // Validate output path
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Directory.GetCurrentDirectory();
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Get TrID version
            var tridVersion = GetTrIDVersion(_scriptPath);
            Console.WriteLine("TrID Package Detected. Version: {0}", tridVersion);

            // Build
            Console.WriteLine("Building definitions...");
            var definitionDictionary = BuildDefinitions(Block, outputPath);
            var definitions = definitionDictionary.Flatten();

            // Build package metadata
            Console.WriteLine("Building package metadata...");
            var metadata = new Metadata
            {
                Source = new TrIDInfo
                {
                    TotalRecords = Block.Sum(kvp => kvp.Value.Count),
                    Version = tridVersion
                },
                Package = new PackageInfo
                {
                    Version = Configuration.Version,
                    CreatedAt = DateTime.UtcNow,
                    TotalDefinitions = definitions.Count,
                    TotalMimeTypes = definitionDictionary.Count,
                }
            };

            // Compile
            ExportToJson(metadata, outputPath);
            Console.WriteLine("Package metadata has been exported to {0}", Configuration.FILEDEF_METADATA_NAME);
            ExportToBinary(definitions, outputPath);
            Console.WriteLine("Package has been exported to {0}", Configuration.FILEDEF_PACKAGE_NAME);
        }

        private Dictionary<string, List<Definition>> BuildDefinitions(Dictionary<int, List<TrIDDefinition>> definitionsBlock, string outputPath)
        {
            // Build the definition collection
            var definitions = definitionsBlock
                .SelectMany(kvp => kvp.Value)
                .Select(trid => new Definition
                {
                    FileType = trid.FileType,
                    Extension = trid.Extension,
                    MimeType = trid.MimeType,
                    Remarks = trid.Remarks,
                    Signature = new Signature
                    {
                        Patterns = trid.Patterns,
                        Strings = trid.Strings,
                    },
                    PriorityLevel = CalculatePriority(trid)
                })
                .OrderByDescending(d => d.PriorityLevel)
                .ToList();

            Console.WriteLine("Validading Definitions...");
            var definitionDictionary = definitions.GroupByMimeType().Cleanse(true);
            var dumpPath = Configuration.GetDumpPath(outputPath);
            definitionDictionary.CreateMemoryDump(dumpPath);
            Console.WriteLine("Memory dump has been created in {0}", dumpPath);

            return definitionDictionary;
        }

        private static int CalculatePriority(TrIDDefinition definition)
        {
            int priority = 0;

            // Higher priority for definitions with patterns at position 0
            if (definition.Patterns.Any(p => p.Position == 0))
                priority += 100;

            // Priority based on number of files used to create definition
            priority += Math.Min(definition.FileCount / 100, 50);

            // Priority based on pattern count
            priority += definition.Patterns.Count * 10;

            return priority;
        }

        private static string GetTrIDVersion(string trIdPath)
        {
            var fileContent = File.ReadAllText(trIdPath);
            // Parse PROGRAM_VER python variable
            var varName = "PROGRAM_VER";
            var searchPattern = $"{varName} = \"";

            var extractedValue = string.Empty;
            var startIndex = fileContent.IndexOf(searchPattern);

            if (startIndex > -1)
            {
                // Move past the variable name and opening quote
                startIndex += searchPattern.Length;
                // Find the closing quote
                var endIndex = fileContent.IndexOf('"', startIndex);

                if (endIndex > -1)
                {
                    extractedValue = fileContent[startIndex..endIndex];
                }
            }

            return extractedValue;
        }

        private static void ExportToJson(Metadata metadata, string outputPath)
        {
            outputPath = Path.Combine(outputPath, Configuration.FILEDEF_METADATA_NAME);

            var json = JsonSerializer.Serialize(metadata, PackageMetadataJsonContext.Default.Metadata);
            File.WriteAllText(outputPath, json);
        }

        private static void ExportToBinary(List<Definition> definitions, string outputPath)
        {
            outputPath = Path.Combine(outputPath, Configuration.FILEDEF_PACKAGE_NAME);

            var package = MessagePackSerializer.Serialize(definitions);
            File.WriteAllBytes(outputPath, package);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Metadata))]
    internal partial class PackageMetadataJsonContext : JsonSerializerContext
    {
    }
}
