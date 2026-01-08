using MessagePack;
using System.Text.Json.Serialization;

namespace Rheo.Storage.Analysing.Models
{
    /// <summary>
    /// Represents a package containing a collection of file type definitions, version information, creation date, and associated tags.
    /// </summary>
    [MessagePackObject]
    public partial class Package
    {
        /// <summary>
        /// Gets or sets the version of the package.
        /// </summary>
        [Key(0)]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC date and time when the package was created.
        /// </summary>
        [Key(1)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the tags that describe the state or characteristics of the package.
        /// </summary>
        [Key(2)]
        public PackageTag Tags { get; set; } = PackageTag.None;

        /// <summary>
        /// Gets the total number of file type definitions in the package.
        /// </summary>
        [JsonIgnore]
        [IgnoreMember]
        public int TotalDefinitions => Definitions.Count;

        /// <summary>
        /// Gets the total number of distinct MIME types represented in the package definitions.
        /// </summary>
        [JsonIgnore]
        [IgnoreMember]
        public int TotalMimeTypes => Definitions.Select(def => def.MimeType).Distinct().Count();

        /// <summary>
        /// Gets or sets the list of file type definitions contained in the package.
        /// </summary>
        [Key(3)]
        public List<Definition> Definitions { get; set; } = [];
    }

    /// <summary>
    /// Represents tags that describe the state or characteristics of a package.
    /// </summary>
    [Flags]
    public enum PackageTag
    {
        /// <summary>
        /// No tag is specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The package is stable and production-ready.
        /// </summary>
        Stable = 1 << 0,

        /// <summary>
        /// The package is in beta and may be subject to changes.
        /// </summary>
        Beta = 1 << 1,

        /// <summary>
        /// The package is deprecated and should not be used.
        /// </summary>
        Deprecated = 1 << 2,

        /// <summary>
        /// The package is experimental and may be unstable.
        /// </summary>
        Experimental = 1 << 3,

        /// <summary>
        /// The package is associated with TrID file type definitions.
        /// </summary>
        TrID = 1 << 4,

        /// <summary>
        /// The package has been validated.
        /// </summary>
        Validated = 1 << 5
    }
}
