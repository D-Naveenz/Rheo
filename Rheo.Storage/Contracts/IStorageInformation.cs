using Rheo.Storage.Information;
using System.Drawing;

namespace Rheo.Storage.Contracts
{
    /// <summary>
    /// Provides detailed information about a storage object, such as a file or directory, including identity, attributes, size, timestamps, links, and platform-specific metadata.
    /// </summary>
    public interface IStorageInformation
    {
        #region Core Identity
        /// <summary>
        /// The user-friendly name for the storage. This is typically the name shown in file explorer.
        /// </summary>
        string DisplayName { get; }

        #endregion

        #region Attributes (FileAttributes enum)
        /// <summary>
        /// The file attributes associated with the storage object (e.g., ReadOnly, Hidden, System, etc.).
        /// </summary>
        FileAttributes Attributes { get; }

        /// <summary>
        /// Indicates whether the storage object is read-only.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Indicates whether the storage object is hidden.
        /// </summary>
        bool IsHidden { get; }

        /// <summary>
        /// Indicates whether the storage object is a system file.
        /// </summary>
        bool IsSystem { get; }

        /// <summary>
        /// Indicates whether the storage object is temporary.
        /// </summary>
        bool IsTemporary { get; }

        #endregion

        #region Size
        /// <summary>
        /// The size of the storage object in bytes.
        /// </summary>
        long Size { get; }

        /// <summary>
        /// The formatted size of the storage object (e.g., "1.2 MB").
        /// </summary>
        string FormattedSize { get; }

        #endregion

        #region Timestamps
        /// <summary>
        /// The creation time of the storage object.
        /// </summary>
        DateTime CreationTime { get; }

        /// <summary>
        /// The last write time of the storage object.
        /// </summary>
        DateTime LastWriteTime { get; }

        /// <summary>
        /// The last access time of the storage object.
        /// </summary>
        DateTime LastAccessTime { get; }

        #endregion

        #region Links & Targets
        /// <summary>
        /// Indicates whether the storage object is a symbolic link.
        /// </summary>
        bool IsSymbolicLink { get; }

        /// <summary>
        /// The target path of the symbolic link, if applicable.
        /// </summary>
        string? LinkTarget { get; }

        #endregion

        #region Platform-Specific (optional/nullable)
        /// <summary>
        /// The security identifier (SID) of the owner (Windows only).
        /// </summary>
        string? OwnerSid { get; }

        /// <summary>
        /// Gets the icon associated with this instance, if available. (Windows only)
        /// </summary>
        Icon? Icon { get; }

        /// <summary>
        /// The user ID of the owner (Unix only).
        /// </summary>
        int? OwnerId { get; }

        /// <summary>
        /// The group ID of the owner (Unix only).
        /// </summary>
        int? GroupId { get; }

        /// <summary>
        /// The file mode (permissions) of the storage object (Unix only).
        /// </summary>
        int? Mode { get; }
        #endregion

        /// <summary>
        /// Returns a string representation of the size, optionally formatted using the specified unit of measurement
        /// (UOM).
        /// </summary>
        /// <remarks>When the <paramref name="uom"/> parameter is provided, the size is formatted using
        /// the specified unit of measurement. If <paramref name="uom"/> is <see langword="null"/>, the method
        /// determines the most appropriate unit (bytes, kilobytes, megabytes, or gigabytes) based on the size in
        /// bytes.</remarks>
        /// <param name="uom">The unit of measurement to use for formatting the size. If <see langword="null"/>, the method automatically
        /// selects an appropriate unit based on the size in bytes.</param>
        /// <returns>A string representing the size, including the unit of measurement. If the size is unknown, the string
        /// "Unknown size" is returned.</returns>
        string GetSizeString(UOM? uom = null);
    }
}
