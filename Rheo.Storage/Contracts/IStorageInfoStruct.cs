namespace Rheo.Storage.Contracts
{
    /// <summary>
    /// Represents the structure containing information about a file or directory in storage.
    /// </summary>
    public interface IStorageInfoStruct
    {
        /// <summary>
        /// Specifies the file or directory attributes as a set of flags.
        /// </summary>
        FileAttributes Attributes { get; set; }

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        ulong Size { get; set; }

        /// <summary>
        /// The creation time of the file or directory in UTC.
        /// </summary>
        DateTime CreationTime { get; set; }

        /// <summary>
        /// The last write (modification) time of the file or directory in UTC.
        /// </summary>
        DateTime LastWriteTime { get; set; }

        /// <summary>
        /// The last access time of the file or directory in UTC.
        /// </summary>
        DateTime LastAccessTime { get; set; }
    }
}
