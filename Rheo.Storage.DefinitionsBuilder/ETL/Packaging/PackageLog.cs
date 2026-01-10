using Rheo.Storage.Analysing.Models;
using System.Diagnostics;
using System.Text;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Packaging
{
    [DebuggerDisplay("PackageLog: {LogType}, MimeCount={MimeCount}, Extensions={ExtensionsCount}")]
    public class PackageLog(string logType)
    {
        private Dictionary<string, HashSet<string>> _extensionsByMime = [];
        private int _definitionCount;

        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string LogType { get; } = logType;
        public int MimeCount => _extensionsByMime.Count;
        public int DefinitionCount => _definitionCount;
        public int ExtensionsCount => _extensionsByMime.Values.Sum(list => list.Count);
        public Dictionary<string, HashSet<string>> ExtensionsByMime => _extensionsByMime;

        /// <summary>
        /// Sets the collection of definitions grouped by MIME type.
        /// </summary>
        /// <param name="definitionsByMime">A dictionary that maps MIME type strings to lists of <see cref="Definition"/> objects.  The key represents
        /// the MIME type, and the value is the list of definitions associated with that type.  If a key is null, empty,
        /// or consists only of whitespace, it is treated as "Unknown".</param>
        public void SetDefinitionsPackage(Dictionary<string, List<Definition>> definitionsByMime)
        {
            _definitionCount = definitionsByMime.Values.Sum(list => list.Count);
            _extensionsByMime = definitionsByMime.ToDictionary(
                kvp => string.IsNullOrWhiteSpace(kvp.Key) ? "Unknown" : kvp.Key,
                kvp => kvp.Value.SelectMany(d => d.Extensions).ToHashSet()
            );
        }

        /// <summary>
        /// Sets the collection of definitions to be used by the package.
        /// </summary>
        /// <param name="definitions">An enumerable collection of <see cref="Definition"/> objects to assign to the package. Cannot be null.</param>
        public void SetDefinitionsPackage(IEnumerable<Definition> definitions)
        {
            SetDefinitionsPackage(definitions.GroupByMimeType());
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Log type: {LogType}");
            sb.AppendLine($"Timestamp: {Timestamp}");
            sb.AppendLine($"Total MIME types: {MimeCount}");
            sb.AppendLine($"Total definitions: {DefinitionCount}");
            sb.AppendLine($"Total extensions: {ExtensionsCount}");
            sb.AppendLine();
            
            foreach (var kvp in _extensionsByMime)
            {
                sb.AppendLine($"MIME Type: {kvp.Key} - Extensions: {kvp.Value.Count}");
                sb.AppendLine(string.Join(", ", kvp.Value));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
