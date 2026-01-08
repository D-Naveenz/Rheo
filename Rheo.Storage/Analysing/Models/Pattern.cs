using MessagePack;
using System.Text;

namespace Rheo.Storage.Analysing.Models
{
    /// <summary>
    /// Represents a pattern with an associated position and data.
    /// </summary>
    /// <remarks>This class is used to store a pattern's offset position within a file and its corresponding
    /// data.</remarks>
    [MessagePackObject]
    public partial class Pattern
    {
        /// <summary>
        /// The offset position of the pattern in the file.
        /// </summary>
        [Key(0)]
        public ushort Position { get; set; }
        /// <summary>
        /// Gets or sets the prefix pattern data.
        /// </summary>
        [Key(1)]
        public byte[] Data { get; set; } = [];

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"\"{Encoding.ASCII.GetString(Data)}\" at {Position}";
        }
    }
}
