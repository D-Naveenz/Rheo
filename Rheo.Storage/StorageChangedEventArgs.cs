using Rheo.Storage.Contracts;

namespace Rheo.Storage
{
    /// <summary>
    /// Provides data for events that signal a change to a storage object, such as creation, modification, deletion, or relocation.
    /// </summary>
    /// <remarks>
    /// Use this class with event handlers to determine which storage object was affected and the type of change that occurred.
    /// The <see cref="NewInfo"/> property contains the storage information for all change types except <see cref="StorageChangeType.Deleted"/>, 
    /// where the storage object may no longer exist and only the change notification is available.
    /// </remarks>
    public class StorageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the most recently retrieved storage information, or null if no information is available.
        /// </summary>
        public IStorageInformation? NewInfo { get; }

        /// <summary>
        /// Gets the type of change that occurred to the storage object.
        /// </summary>
        public StorageChangeType ChangeType { get; }

        /// <summary>
        /// Initializes a new instance of the StorageChangedEventArgs class with the specified change type and storage
        /// information.
        /// </summary>
        /// <param name="changeType">The type of change that occurred in the storage. Indicates whether the storage was created, modified, or
        /// deleted.</param>
        /// <param name="storageInformation">The storage information associated with the change. Can be null only if the change type is Deleted.</param>
        /// <exception cref="ArgumentNullException">Thrown if storageInformation is null and changeType is not Deleted.</exception>
        public StorageChangedEventArgs(StorageChangeType changeType, IStorageInformation? storageInformation)
        {
            if (changeType != StorageChangeType.Deleted && storageInformation is null)
            {
                throw new ArgumentNullException(nameof(storageInformation), 
                    $"Storage information cannot be null when change type is {changeType}.");
            }

            ChangeType = changeType;
            NewInfo = storageInformation;
        }
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
