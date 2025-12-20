using Rheo.Storage.DefinitionsBuilder.ETL.Packaging;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Rheo.Storage.DefinitionsBuilder.Settings
{
    internal sealed class PackageCommand : Command<PackageCommand.PackageSettings>
    {
        private const string SourcePackageExtension = ".trd";
        private const string DefaultPackagePath = @"Data\TrID\triddefs";
        private const string DefaultOutputPath = "Output";

        public sealed class PackageSettings : CommandSettings
        {
            [Description("Path to the package to extract definitions from.")]
            [CommandArgument(0, "[packagepath]")]
            public string? PackagePath { get; set; }

            [Description("Output path for the generated definitions.")]
            [CommandOption("-o|--output")]
            [DefaultValue(DefaultOutputPath)]
            public string OutputPath { get; set; } = string.Empty;
        }

        public override int Execute(CommandContext context, PackageSettings settings, CancellationToken cancellationToken)
        {
            // Build Definitions Package
            var package = PackageBuilder.Build();
            // Export Package
            Exporter.ExportPackage(package, settings.OutputPath);
            // Save Package Log
            Exporter.SavePackageLogs("Logs", package.Logs);

            Console.WriteLine("\nDatabase ready for Rheo Storage library!");
            return 0;
        }

        public override ValidationResult Validate(CommandContext context, PackageSettings settings)
        {
            // Validate Package Path
            string packagePath;
            if (string.IsNullOrWhiteSpace(settings.PackagePath))
            {
                packagePath = Path.GetFullPath(DefaultPackagePath + SourcePackageExtension);
                if (!File.Exists(packagePath))
                {
                    return ValidationResult.Error($"Package path is required. Default package not found at '{packagePath}'.");
                }
            }
            else
            {
                packagePath = Path.GetFullPath(settings.PackagePath);
                if (!File.Exists(packagePath))
                {
                    return ValidationResult.Error($"The specified package path does not exist: '{packagePath}'.");
                }
                if (!Path.GetExtension(packagePath).Equals(SourcePackageExtension, StringComparison.CurrentCultureIgnoreCase))
                {
                    return ValidationResult.Error($"The specified package is not a TrID file definitions package");
                }
            }
            settings.PackagePath = packagePath;

            // Validate Output Path
            settings.OutputPath = string.IsNullOrWhiteSpace(settings.OutputPath)? 
                Path.Combine(Directory.GetCurrentDirectory(), "Output") : 
                Path.GetFullPath(settings.OutputPath);

            return base.Validate(context, settings);
        }
    }
}
