using MessagePack;
using Rheo.Storage.FileDefinition.Models;
using Rheo.Storage.Properties;

namespace Rheo.Storage.FileDefinition
{
    internal class FileDefinitions
    {
        private static readonly Lazy<Package> _packageLoader = new(LoadDefinitionsPackage);
        
        // Strategy 1: TrID-compatible (position-0 only, simpler)
        private static readonly Lazy<Dictionary<int, List<Definition>>> _headersByteMapLoader
            = new(() => BuildHeadersByteMap(_packageLoader.Value.Definitions));
        
        // Strategy 2: Comprehensive (all patterns, more flexible)
        private static readonly Lazy<Dictionary<int, List<PatternDefinitionMap>>> _allPatternsByteMapLoader
            = new(() => BuildAllPatternsByteMap(_packageLoader.Value.Definitions));
        
        // Essential for extension-based lookups
        private static readonly Lazy<Dictionary<string, List<Definition>>> _extensionMapLoader
            = new(() => BuildExtensionMap(_packageLoader.Value.Definitions));

        /// <summary>
        /// Gets the current instance of the package loaded by the application.
        /// </summary>
        public static Package Package => _packageLoader.Value;
        
        /// <summary>
        /// Maps first byte of position-0 patterns to definitions (TrID-compatible).
        /// Use for fast, simple header-based detection.
        /// </summary>
        public static Dictionary<int, List<Definition>> HeadersByteMap => _headersByteMapLoader.Value;
        
        /// <summary>
        /// Maps first byte of ALL patterns to pattern-definition pairs.
        /// Use for comprehensive multi-pattern analysis.
        /// </summary>
        public static Dictionary<int, List<PatternDefinitionMap>> AllPatternsByteMap => _allPatternsByteMapLoader.Value;
        
        /// <summary>
        /// Maps file extensions to definitions.
        /// </summary>
        public static Dictionary<string, List<Definition>> ExtensionMap => _extensionMapLoader.Value;

        private static Package LoadDefinitionsPackage()
        {
            try
            {
                var package = MessagePackSerializer.Deserialize<Package>(Resources.FileDefinitions);
                return package!;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Builds a mapping from header byte values to lists of definitions based on the first byte of each
        /// definition's signature pattern.
        /// </summary>
        /// <remarks>Definitions whose signature patterns do not specify a first byte are grouped under
        /// the key -1. The returned dictionary includes all possible byte values from -1 to 255 as keys, each
        /// associated with a list of matching definitions, which may be empty.</remarks>
        /// <param name="definitions">A list of definitions to be mapped by their signature's first byte.</param>
        /// <returns>A dictionary where each key is a byte value (or -1 for definitions without a first-byte pattern), and each
        /// value is a list of definitions that correspond to that byte.</returns>
        private static Dictionary<int, List<Definition>> BuildHeadersByteMap(List<Definition> definitions)
        {
            var map = new Dictionary<int, List<Definition>>();
            for (int i = -1; i <= byte.MaxValue; i++)
            {
                map[i] = [];
            }

            foreach (var definition in definitions)
            {
                var patterns = definition.Signature.Patterns;
                if (patterns.Count > 0)
                {
                    var firstPattern = patterns.OrderBy(p => p.Position).FirstOrDefault();
                    if (firstPattern != null && firstPattern.Position == 0 && firstPattern.Data.Length > 0)
                    {
                        var firstByte = firstPattern.Data[0];
                        map[firstByte].Add(definition);
                        continue;
                    }
                }
                
                map[-1].Add(definition);
            }

            return map;
        }

        /// <summary>
        /// Builds a mapping from the first byte of each pattern to a list of pattern-definition associations.
        /// </summary>
        /// <remarks>Patterns with no data or definitions with no patterns are mapped under the key -1.
        /// The returned dictionary contains entries for all possible byte values (0 to 255) as well as -1, each
        /// initialized with an empty list if no patterns are associated.</remarks>
        /// <param name="definitions">A list of definitions containing signature patterns to be mapped. Cannot be null.</param>
        /// <returns>A dictionary where each key is an integer representing a possible first byte value (or -1 for patterns with
        /// no data), and each value is a list of pattern-definition mappings associated with that byte.</returns>
        private static Dictionary<int, List<PatternDefinitionMap>> BuildAllPatternsByteMap(List<Definition> definitions)
        {
            var map = new Dictionary<int, List<PatternDefinitionMap>>();
            for (int i = -1; i <= byte.MaxValue; i++)
            {
                map[i] = [];
            }

            foreach (var definition in definitions)
            {
                if (definition.Signature.Patterns.Count == 0)
                {
                    map[-1].Add(new PatternDefinitionMap
                    {
                        Definition = definition
                    });
                    continue;
                }

                foreach (var pattern in definition.Signature.Patterns)
                {
                    if (pattern.Data.Length > 0)
                    {
                        var firstByte = pattern.Data[0];
                        map[firstByte].Add(new PatternDefinitionMap
                        {
                            Pattern = pattern,
                            Definition = definition
                        });
                    }
                    else
                    {
                        // Pattern with no data
                        map[-1].Add(new PatternDefinitionMap
                        {
                            Pattern = pattern,
                            Definition = definition
                        });
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Builds a mapping from file extension to the list of definitions that support each extension.
        /// </summary>
        /// <remarks>If multiple definitions share the same extension, all are included in the
        /// corresponding list. Extension matching is case-insensitive and ignores leading dots.</remarks>
        /// <param name="definitions">A list of definitions, each containing one or more file extensions.</param>
        /// <returns>A dictionary that maps each normalized file extension (without leading dot, case-insensitive) to a list of
        /// definitions associated with that extension.</returns>
        private static Dictionary<string, List<Definition>> BuildExtensionMap(List<Definition> definitions)
        {
            var map = new Dictionary<string, List<Definition>>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in definitions)
            {
                foreach (var ext in definition.Extensions)
                {
                    var normalizedExt = ext.TrimStart('.').ToLowerInvariant();
                    
                    if (!map.TryGetValue(normalizedExt, out var list))
                    {
                        list = [];
                        map[normalizedExt] = list;
                    }
                    
                    list.Add(definition);
                }
            }

            return map;
        }
    }
}
