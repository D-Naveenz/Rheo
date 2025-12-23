using Rheo.Storage.DefinitionsBuilder.ETL.RIFF;
using Rheo.Storage.DefinitionsBuilder.ETL.Sluice;
using Rheo.Storage.DefinitionsBuilder.ETL.Validation;
using Rheo.Storage.DefinitionsBuilder.Settings;
using Rheo.Storage.MIME.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Packaging
{
    internal class PackageBuilder
    {
        public const string PACKAGE_NAME = "triddefs.trd";

        public List<PackageLog> Logs { get; set; } = [];

        /// <summary>
        /// Builds a new <see cref="Package"/> instance by extracting, validating, and filtering definitions.
        /// </summary>
        /// <remarks>The returned package includes only definitions that have passed MIME type validation
        /// and filtering steps.  The package is tagged as both <see cref="PackageTag.TrID"/> and <see
        /// cref="PackageTag.Validated"/>.  A log of the valid definitions is attached to the package for auditing or
        /// diagnostic purposes.</remarks>
        /// <returns>A <see cref="Package"/> containing the validated and filtered definitions, along with associated logs.</returns>
        public Package Build()
        {
            List<PackageLog> packageLogs = [];

            Console.WriteLine("Starting package building process...");

            // Extract
            var rawDefinitions = BuildRawDefinitions();
            // Transform
            var validatedDefinitions = ValidateMimeTypes(rawDefinitions, packageLogs);
            var filteredDefinitions = FilterDefinitions(validatedDefinitions, packageLogs);
            filteredDefinitions = [.. filteredDefinitions.OrderByDescending(d => d.PriorityLevel)];
            // Load
            var package = new Package
            {
                Tags = PackageTag.TrID | PackageTag.Validated,
                Definitions = filteredDefinitions
            };

            // Create logs
            var validLog = new PackageLog("ValidDefinitions");
            validLog.SetDefinitionsPackage(filteredDefinitions);
            packageLogs.Add(validLog);
            Logs = packageLogs;

            Console.WriteLine("Package building process completed.");
            Console.WriteLine("Final Package: {0} definitions | {1} MIME types", package.TotalDefinitions, package.TotalMimeTypes);
            return package;
        }

        private static List<Definition> BuildRawDefinitions()
        {
            Console.WriteLine("Reading definitions source package...");
            List<Definition> definitions = [];

            try
            {
                var packagePath = GetTrIDPackagePath(Configuration.TridLocation);
                var block = TridPackageParser.ParsePackage(packagePath);

                // Build the definition collection
                definitions = [.. block
                    .SelectMany(kvp => kvp.Value)
                    .Select(trid => new Definition
                    {
                        FileType = trid.FileType,
                        Extensions = trid.Extension.Split('/', StringSplitOptions.RemoveEmptyEntries),
                        MimeType = trid.MimeType,
                        Remarks = trid.Remarks,
                        Signature = new Signature
                        {
                            Patterns = trid.Patterns,
                            Strings = trid.Strings,
                        }
                    })];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while building definitions: {0}", ex.Message);
                throw;
            }

            Console.WriteLine("Successfully read {0} definitions.", definitions.Count);
            return definitions;
        }

        private static List<Definition> ValidateMimeTypes(IEnumerable<Definition> definitions, List<PackageLog> logs)
        {
            Console.WriteLine("Validating definitions...");

            // Validate MIME types
            var (validDefs, invalidDefs) = definitions.Validate(true);

            // Create logs
#if DEBUG
            var validLog = new PackageLog("validMimeTypes");
            validLog.SetDefinitionsPackage(validDefs);
            logs.Add(validLog);
#endif
            var invalidLog = new PackageLog("InvalidMimeTypes");
            invalidLog.SetDefinitionsPackage(invalidDefs);
            logs.Add(invalidLog);

            Console.WriteLine("Mime types have been cleansed.");
            Console.WriteLine("Result: {0} valid definitions | {1} invalid definitions", 
                validDefs.Values.Sum(list => list.Count), 
                invalidDefs.Values.Sum(list => list.Count)
            );
            return validDefs.Flatten();
        }

        private static List<Definition> FilterDefinitions(IEnumerable<Definition> definitions, List<PackageLog> logs)
        {
            Console.WriteLine("Filtering definitions...");

            // Filter definitions by extension levels
            var sluice = definitions.GroupByExtensionLevel();
            var validDefinitions = sluice
                .Where(kvp => kvp.Key > 0 && kvp.Key <= 5)
                .SelectMany(kvp => kvp.Value)
                .ToList();
            var invalidDefinitions = sluice[0];

            // Create logs
            var invalidLog = new PackageLog("FilteredInvalidDefinitions");
            invalidLog.SetDefinitionsPackage(invalidDefinitions);
            logs.Add(invalidLog);

            Console.WriteLine("Definitions have been filtered by extension levels.");
            Console.WriteLine("Result: {0} valid definitions | {1} invalid definitions", 
                validDefinitions.Count, 
                invalidDefinitions.Count);
            return validDefinitions;
        }

        private static string GetTrIDPackagePath(string basePath)
        {
            var packagePath = Path.GetFullPath(Path.Combine(basePath, PACKAGE_NAME));
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException("Couldn't find the TrID data package in the Data Folder.", PACKAGE_NAME);
            }
            return packagePath;
        }
    }
}
