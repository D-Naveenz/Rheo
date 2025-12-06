using MessagePack;
using Rheo.Storage.DefinitionsBuilder.Generation;
using Rheo.Storage.DefinitionsBuilder.Models.Build;
using Rheo.Storage.DefinitionsBuilder.Models.Definition;
using Rheo.Storage.DefinitionsBuilder.Models.Package;
using Rheo.Storage.DefinitionsBuilder.RIFF;
using Rheo.Storage.DefinitionsBuilder.RIFF.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rheo.Storage.DefinitionsBuilder
{
    public class PackageCompiler
    {
        private readonly string _tridVersion;
        private readonly string _packagePath;

        public Dictionary<int, List<TrIDDefinition>> Block { get; }
        public Metadata CollectionMetadata { get; private set; } = new();
        public List<Definition> Definitions { get; private set; } = [];

        public PackageCompiler()
        {
            // Verify paths
            var errormessage = "Couldn't find the TrID - File Identifier or the data package in the Data Folder.";
            var scriptPath = Path.GetFullPath(Path.Combine(Configuration.TridLocation, Configuration.TRID_SCRIPT_NAME));
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException(errormessage, Configuration.TRID_SCRIPT_NAME);
            }
            _packagePath = Path.GetFullPath(Path.Combine(Configuration.TridLocation, Configuration.TRID_PACKAGE_NAME));
            if (!File.Exists(_packagePath))
            {
                throw new FileNotFoundException(errormessage, Configuration.TRID_PACKAGE_NAME);
            }

            // Configure metadata
            _tridVersion = GetTrIDVersion(scriptPath);

            // Parse TrID package
            Block = TridPackageParser.ParsePackage(_packagePath);

            Console.WriteLine("TrID Package Detected. Version: {0}", _tridVersion);
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

            // Build
            BuildDefinitions(Block, outputPath);

            // Compile
            ExportToJson(outputPath);
            ExportToBinary(outputPath);
        }

        private void BuildDefinitions(Dictionary<int, List<TrIDDefinition>> definitionsBlock, string outputPath)
        {
            // Flatten all definitions
            var allDefinitions = definitionsBlock.SelectMany(kvp => kvp.Value).ToList();

            // Build the definition collection
            Definitions = [.. allDefinitions
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
                .OrderByDescending(d => d.PriorityLevel)];

            var defDict = Definitions.GroupByMimeType().Cleanse();

            var packageVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            var categories = BuildCategories(Definitions, Configuration.GetDumpPath(outputPath));

            // Build package metadata
            CollectionMetadata = new Metadata
            {
                DefinitionsInfo = new TrIDInfo
                {
                    TotalRecords = allDefinitions.Count,
                    Version = _tridVersion
                },
                PackageInfo = new PackageInfo
                {
                    Version = packageVersion ?? "1.0",
                    CreatedAt = DateTime.UtcNow,
                    TotalDefinitions = Definitions.Count,
                },
                Categories = categories
            };
        }

        private static List<string> BuildCategories(List<Definition> definitions, string? dumpPath = null)
        {
            var mimetypes = definitions
                .GroupBy(d => d.MimeType)
                .Select(g => new
                {
                    MimeType = g.Key,
                    Extensions = g.Select(d => d.Extension).ToList()
                })
                .ToList();

            var categories = mimetypes
                .GroupBy(m => m.MimeType.Split('/')[0])
                .Select(g => new MemoryDump
                {
                    Category = g.Key,
                    MimeTypes = [.. g.Select(m => m.MimeType)],
                    Extentions = [.. g.SelectMany(m => m.Extensions).Distinct()]
                })
                .ToList();

            // Export memory dump if output path is provided
            if (!string.IsNullOrWhiteSpace(dumpPath))
            {
                dumpPath = Path.GetFullPath(dumpPath);
                if (!Directory.Exists(dumpPath))
                {
                    Directory.CreateDirectory(dumpPath);
                }

                var dumpFilePath = Path.Combine(dumpPath, "memorydump.json");
                var json = JsonSerializer.Serialize(categories, MemoryDumpJsonContext.Default.ListMemoryDump);
                File.WriteAllText(dumpFilePath, json);
                Console.WriteLine("Memory dump has been created in {0}", dumpFilePath);
            }

            return [.. categories.Select(c => c.Category)];
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

        private void ExportToJson(string outputPath)
        {
            outputPath = Path.Combine(outputPath, Configuration.FILEDEF_METADATA_NAME);

            var json = JsonSerializer.Serialize(CollectionMetadata, PackageMetadataJsonContext.Default.Metadata);
            File.WriteAllText(outputPath, json);

            Console.WriteLine("Package metadata has been exported to {0}", Configuration.FILEDEF_METADATA_NAME);
        }

        private void ExportToBinary(string outputPath)
        {
            outputPath = Path.Combine(outputPath, Configuration.FILEDEF_PACKAGE_NAME);

            var package = MessagePackSerializer.Serialize(Definitions);
            File.WriteAllBytes(outputPath, package);
            
            Console.WriteLine("Package has been exported to {0}", Configuration.FILEDEF_PACKAGE_NAME);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Metadata))]
    internal partial class PackageMetadataJsonContext : JsonSerializerContext
    {
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<MemoryDump>))]
    internal partial class MemoryDumpJsonContext : JsonSerializerContext
    {
    }
}
