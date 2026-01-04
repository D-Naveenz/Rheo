namespace Rheo.Storage
{
    /// <summary>
    /// Provides data for storage object change events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StorageChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="absolutePath">The absolute path of the storage object that changed.</param>
    /// <param name="changeType">The type of change that occurred.</param>
    public class StorageChangedEventArgs(string absolutePath, StorageChangeType changeType) : EventArgs
    {
        /// <summary>
        /// Gets the absolute path of the storage object that changed.
        /// </summary>
        public string AbsolutePath { get; } = absolutePath ?? throw new ArgumentNullException(nameof(absolutePath));

        /// <summary>
        /// Gets the type of change that occurred to the storage object.
        /// </summary>
        public StorageChangeType ChangeType { get; } = changeType;
    }

    /// <summary>
    /// Specifies the type of change that occurred to a storage object.
    /// </summary>
    public enum StorageChangeType
    {
        /// <summary>
        /// The storage object was created.
        /// </summary>
        Created,

        /// <summary>
        /// The storage object was deleted.
        /// </summary>
        Deleted,

        /// <summary>
        /// The storage object was modified.
        /// </summary>
        Modified,

        /// <summary>
        /// Indicates whether the item has been relocated, such as moved or renamed.
        /// </summary>
        Relocated
    }
}
