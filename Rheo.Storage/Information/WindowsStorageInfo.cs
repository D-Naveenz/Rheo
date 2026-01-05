using Rheo.Storage.Contracts;
using System.Drawing;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Represents detailed information about a file or directory on a Windows file system.
    /// </summary>
    /// <remarks>This structure provides comprehensive file metadata obtained directly from Windows
    /// APIs, including attributes, size, timestamps, security information, and Shell-provided display information.
    /// Icon handles returned in this structure must be destroyed using DestroyIcon when no longer needed.</remarks>
    public struct WindowsStorageInfo : IStorageInfoStruct
    {
        /// <inheritdoc/>
        public FileAttributes Attributes { get; set; }

        /// <inheritdoc/>
        public ulong Size { get; set; }

        /// <inheritdoc/>
        public DateTime CreationTime { get; set; }

        /// <inheritdoc/>
        public DateTime LastWriteTime { get; set; }

        /// <inheritdoc/>
        public DateTime LastAccessTime { get; set; }

        /// <summary>
        /// The display name for the file as provided by Windows Shell.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The type name (e.g., "Text Document") as provided by Windows Shell.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Handle to the icon representing this file. Must be destroyed with DestroyIcon when done.
        /// </summary>
        public Icon Icon { get; set; }

        /// <summary>
        /// The security identifier (SID) of the file owner.
        /// </summary>
        public string? OwnerSid { get; set; }

        /// <summary>
        /// The number of hard links to the file.
        /// </summary>
        public uint HardLinkCount { get; set; }

        /// <summary>
        /// The volume serial number of the volume containing the file.
        /// </summary>
        public uint VolumeSerialNumber { get; set; }

        /// <summary>
        /// The file index (unique identifier on the volume).
        /// </summary>
        public ulong FileIndex { get; set; }

        /// <summary>
        /// The target path if the file is a reparse point (symbolic link, junction, etc.); otherwise, null.
        /// </summary>
        public string? ReparseTarget { get; set; }
    }
}
