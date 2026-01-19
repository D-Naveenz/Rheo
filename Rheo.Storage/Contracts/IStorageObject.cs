namespace Rheo.Storage.Contracts
{
    /// <summary>
    /// Represents a general abstraction for a storage object, such as a file or directory, providing access to
    /// metadata, path information, and change notifications.
    /// </summary>
    /// <remarks>Implementations of this interface provide a unified way to interact with storage entities,
    /// allowing consumers to retrieve metadata, monitor changes, and manage resource lifetimes. The interface supports
    /// event-driven notifications for content changes and enforces proper disposal patterns. Thread safety and event
    /// handling behavior may vary by implementation; callers should consult specific documentation for
    /// details.</remarks>
    public interface IStorageObject : IDisposable
    {
        /// <summary>
        /// Gets metadata information about the storage object, such as size, attributes, and timestamps.
        /// </summary>
        IStorageInformation Information { get; }

        /// <summary>
        /// Gets the name of the storage object, typically the file or directory name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the full path of the parent directory for the current file or directory.
        /// </summary>
        string ParentDirectory { get; }

        /// <summary>
        /// Gets the full path of the file or directory.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Gets a value indicating whether this storage object has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Occurs when the storage content changes.
        /// </summary>
        /// <remarks>Subscribers are notified whenever an item is added, removed, or updated in the
        /// storage. The event provides details about the change through the <see cref="StorageChangedEventArgs"/>
        /// parameter. This event is typically raised on the thread where the change occurs; callers should ensure
        /// thread safety when handling the event.</remarks>
        event EventHandler<StorageChangedEventArgs>? Changed;

        /// <summary>
        /// Calculates the recommended buffer size based on the current storage information.
        /// </summary>
        /// <returns>An integer representing the recommended buffer size.</returns>
        int GetBufferSize();

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this storage object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object has already been disposed.</exception>
        void ThrowIfDisposed();
    }
}