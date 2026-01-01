using MessagePack;

namespace Rheo.Storage.FileDefinition.Models
{
    /// <summary>
    /// Represents a type definition that includes metadata such as file type, extension, MIME type, and additional
    /// context information.
    /// </summary>
    /// <remarks>This class is used to encapsulate metadata and properties associated with a specific type
    /// definition.  It includes details such as the file type, file extension, MIME type, and priority level, as well
    [MessagePackObject]
    public partial class Definition
    {
        /// <summary>
        /// Gets or sets the file type associated with the type definition.
        /// </summary>
        [Key(0)]
        public string FileType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the file extension associated with the type definition.
        /// </summary>
        [Key(1)]
        public string[] Extensions { get; set; } = [];
        /// <summary>
        /// Gets or sets the MIME type associated with the content.
        /// </summary>
        [Key(2)]
        public string MimeType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets additional information or notes related to the current context.
        /// </summary>
        [Key(3)]
        public string Remarks { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the digital signature associated with the current object.
        /// </summary>
        [Key(4)]
        public Signature Signature { get; set; } = new();
        /// <summary>
        /// Gets or sets the priority level of the definition.
        /// </summary>
        [Key(5)]
        public int PriorityLevel { get; set; } = 0;

        public override string ToString()
        {
            return $"{FileType} ({MimeType}) [{string.Join(", ", Extensions)}]";
        }
    }
}
