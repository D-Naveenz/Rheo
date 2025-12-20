using MessagePack;
using Rheo.Storage.DefinitionsBuilder.Settings;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Packaging
{
    internal class Exporter
    {
        /// <summary>
        /// Exports the specified <see cref="Package"/> to the given directory in both JSON and binary formats.
        /// </summary>
        /// <remarks>This method exports the package data to the specified directory in both JSON and
        /// binary file formats.  If the target directory does not exist, it will be created automatically. Existing
        /// files in the directory may be overwritten.</remarks>
        /// <param name="package">The <see cref="Package"/> instance to export. Cannot be <c>null</c>.</param>
        /// <param name="outputPath">The path to the directory where the exported files will be saved. If the directory does not exist, it will
        /// be created.</param>
        /// <exception cref="IOException">Thrown if the export operation fails, such as when the output path is invalid or inaccessible.</exception>
        public static void ExportPackage(Package package, string outputPath)
        {
            try
            {
                var fullOutputPath = Path.GetFullPath(outputPath);
                if (!Directory.Exists(fullOutputPath))
                {
                    Directory.CreateDirectory(fullOutputPath);
                }

                // Export to JSON
                ExportToJson(package, fullOutputPath);
                // Export to Binary
                ExportToBinary(package, fullOutputPath);

                Console.WriteLine($"Package has been exported to '{fullOutputPath}'.");
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to export package to '{outputPath}'.", ex);
            }
        }

        /// <summary>
        /// Saves the specified package log to a file at the given path.
        /// </summary>
        /// <param name="logPath">The directory path where the package log file will be saved. The path must be valid and accessible.</param>
        /// <param name="packageLog">The <see cref="PackageLog"/> instance to save. Cannot be <see langword="null"/>.</param>
        /// <exception cref="IOException">Thrown if the log cannot be saved to the specified path, such as when the path is invalid or inaccessible.</exception>
        public static void SavePackageLog(string logPath, PackageLog packageLog)
        {
            try
            {
                logPath = Path.GetFullPath(logPath);
                logPath = logPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                var fullLogPath = GetLogPath(packageLog.LogType, logPath);
                File.WriteAllText(fullLogPath, packageLog.ToString());
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save package log to '{logPath}'.", ex);
            }
        }

        /// <summary>
        /// Saves a collection of package logs to files at the specified directory path.
        /// </summary>
        /// <remarks>Each <see cref="PackageLog"/> in the collection is written to a separate file
        /// determined by its log type. Existing files with the same name will be overwritten.</remarks>
        /// <param name="logPath">The directory path where the package log files will be saved. Must be a valid, accessible file system path.</param>
        /// <param name="packageLogs">A collection of <see cref="PackageLog"/> instances to be saved. Each log will be written to a separate file
        /// based on its log type.</param>
        /// <exception cref="IOException">Thrown if an I/O error occurs while saving the package logs, such as if the path is invalid or inaccessible.</exception>
        public static void SavePackageLogs(string logPath, IEnumerable<PackageLog> packageLogs)
        {
            try
            {
                logPath = Path.GetFullPath(logPath);
                logPath = logPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                foreach (var packageLog in packageLogs)
                {
                    var fullLogPath = GetLogPath(packageLog.LogType, logPath);
                    File.WriteAllText(fullLogPath, packageLog.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save package log to '{logPath}'.", ex);
            }
        }

        private static string GetLogPath(string baseName, string logPath, int count = 0)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            var path = Path.Combine(logPath, $"{baseName}_{timestamp}");
            if (count == 0)
            {
                path += ".log";
            }
            else
            {
                path += $"_{count}.log";
            }

            if (!Directory.Exists(logPath))
            {
                throw new DirectoryNotFoundException($"Log path '{logPath}' does not exist.");
            }

            if (!File.Exists(path))
            {
                return path;
            }

            return GetLogPath(baseName, logPath, count + 1);
        }

        /// <summary>
        /// Exports the specified <see cref="Package"/> instance to a JSON file at the given output path.
        /// </summary>
        /// <remarks>The method serializes the <paramref name="package"/> to JSON using the default
        /// serialization context and writes the result to a file in the specified directory.  If a file with the same
        /// name already exists, it will be overwritten.</remarks>
        /// <param name="package">The <see cref="Package"/> object to serialize and export. Cannot be <see langword="null"/>.</param>
        /// <param name="outputPath">The directory path where the JSON file will be created. The file name and extension are determined
        /// automatically.</param>
        /// <exception cref="IOException">Thrown if the export operation fails, such as when the file cannot be written to the specified path.</exception>
        private static void ExportToJson(Package package, string outputPath)
        {
            try
            {
                outputPath = Path.Combine(outputPath, Configuration.FILEDEF_PACKAGE_NAME + ".json");

                var json = JsonSerializer.Serialize(package, PackageJsonContext.Default.Package);
                File.WriteAllText(outputPath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to export package to JSON at '{outputPath}'.", ex);
            }
        }

        /// <summary>
        /// Exports the specified <see cref="Package"/> to a binary file in MessagePack format at the given output path.
        /// </summary>
        /// <param name="package">The <see cref="Package"/> instance to serialize and export. Cannot be <c>null</c>.</param>
        /// <param name="outputPath">The directory path where the binary file will be created. The method combines this path with the package
        /// file name and the ".rpkg" extension.</param>
        /// <exception cref="IOException">Thrown if the export operation fails, such as when the file cannot be written or an I/O error occurs.</exception>
        private static void ExportToBinary(Package package, string outputPath)
        {
            try
            {
                outputPath = Path.Combine(outputPath, Configuration.FILEDEF_PACKAGE_NAME + ".rpkg");

                var binary = MessagePackSerializer.Serialize(package);
                File.WriteAllBytes(outputPath, binary);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to export package to binary at '{outputPath}'.", ex);
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Package))]
    internal partial class PackageJsonContext : JsonSerializerContext
    {
    }
}
