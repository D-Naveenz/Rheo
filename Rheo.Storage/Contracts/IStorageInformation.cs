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

        /// <summary>
        /// The user-friendly description of the type. This is typically a description of the storage type (e.g., "Text Document").
        /// </summary>
        string TypeName { get; }

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
        ulong Size { get; }

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
        /// The handle to the icon associated with the storage object (Windows only).
        /// </summary>
        nint? IconHandle { get; }

        /// <summary>
        /// The user ID of the owner (Unix only).
        /// </summary>
        uint? OwnerId { get; }

        /// <summary>
        /// The group ID of the owner (Unix only).
        /// </summary>
        uint? GroupId { get; }

        /// <summary>
        /// The file mode (permissions) of the storage object (Unix only).
        /// </summary>
        uint? Mode { get; }
        #endregion
    }
}
