using MessagePack;
using Rheo.Storage.DefinitionsBuilder.Models;
using Rheo.Storage.DefinitionsBuilder.Settings;
using System.Text.Json.Serialization;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Packaging
{
    [MessagePackObject]
    public partial class Package
    {
        [Key(0)]
        public string Version { get; set; } = Configuration.Version;
        [Key(1)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Key(2)]
        public PackageTag Tags { get; set; } = PackageTag.None;
        [JsonIgnore]
        [IgnoreMember]
        public int TotalDefinitions => Definitions.Count;
        [JsonIgnore]
        [IgnoreMember]
        public int TotalMimeTypes => Definitions.Select(def => def.MimeType).Distinct().Count();
        [Key(3)]
        public List<Definition> Definitions { get; set; } = [];
        [JsonIgnore]
        [IgnoreMember]
        public List<PackageLog> Logs { get; set; } = [];
    }

    [Flags]
    public enum PackageTag
    {
        None = 0,
        Stable = 1 << 0,
        Beta = 1 << 1,
        Deprecated = 1 << 2,
        Experimental = 1 << 3,
        TrID = 1 << 4,
        Validated = 1 << 5
    }
}
