using Rheo.Storage.Contracts;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Represents detailed information about a file or directory on a Unix-based file system.
    /// </summary>
    /// <remarks>This structure provides file metadata commonly retrieved from Unix file system
    /// queries, including attributes, size, timestamps, ownership, permissions, and symbolic link information. Some
    /// fields may have platform-specific interpretations; for example, OwnerId and GroupId correspond to user and
    /// group IDs on Unix systems, while Mode reflects Unix file permissions and type. Not all fields may be
    /// meaningful or populated on non-Unix platforms.</remarks>
    public struct UnixStorageInfo : IStorageInfoStruct
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
        /// The owner identifier. On Unix, this is the user ID (UID); on Windows, this may be a partial SID.
        /// </summary>
        public uint OwnerId { get; set; }

        /// <summary>
        /// The group identifier. On Unix, this is the group ID (GID).
        /// </summary>
        public uint GroupId { get; set; }

        /// <summary>
        /// The file mode (permissions and type). On Unix, this is the st_mode field.
        /// </summary>
        public uint Mode { get; set; }

        /// <summary>
        /// The target path if the file is a symbolic link; otherwise, null.
        /// </summary>
        public string? SymlinkTarget { get; set; }
    }
}
