using Rheo.Storage.Information;

namespace Rheo.Storage.Contracts
{
    /// <summary>
    /// Represents a file-based storage object that provides file-specific operations such as copying, moving, renaming,
    /// deleting, and writing data, both synchronously and asynchronously.
    /// </summary>
    /// <remarks>The IFileObject interface extends <see cref="IStorageObject"/> to provide operations specific to files,
    /// including support for progress reporting and cancellation in asynchronous methods. Implementations may vary in
    /// behavior depending on the underlying storage provider, particularly regarding overwrite behavior, thread safety,
    /// and support for progress reporting. All methods that accept a cancellation token honor cancellation requests
    /// where supported. Thread safety for write operations is ensured by internal synchronization mechanisms in
    /// compliant implementations.</remarks>
    public interface IFileObject : IStorageObject
    {
        /// <summary>
        /// Gets the file-specific information associated with this instance.
        /// </summary>
        /// <remarks>This property returns the underlying information as a FileInformation object. If the
        /// base information is not of type FileInformation, an InvalidOperationException is thrown.</remarks>
        new FileInformation Information { get; }

        /// <summary>
        /// Creates a copy of the current file at the specified destination path.
        /// </summary>
        /// <param name="destination">The full path where the file copy will be created. This must include the file name and extension.</param>
        /// <param name="overwrite">A value indicating whether to overwrite the file at the destination path if it already exists. Set to <see
        /// langword="true"/> to overwrite; otherwise, <see langword="false"/>.</param>
        /// <returns>A <see cref="IFileObject"/> representing the newly created copy of the file.</returns>
        IFileObject Copy(string destination, bool overwrite);

        /// <summary>
        /// Creates a copy of the current file at the specified destination path.
        /// </summary>
        /// <remarks>If a file already exists at the destination and overwrite is false, the method may
        /// throw an exception or fail to copy the file, depending on the implementation. The copy operation may be
        /// performed asynchronously depending on the underlying storage provider.</remarks>
        /// <param name="destination">The full path where the file will be copied. This must include the file name and extension. Cannot be null
        /// or empty.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation. May be null if progress
        /// reporting is not required.</param>
        /// <param name="overwrite">true to overwrite the destination file if it already exists; otherwise, false.</param>
        /// <returns>A <see cref="IFileObject"/> representing the newly copied file at the destination path.</returns>
        IFileObject Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false);

        /// <summary>
        /// Asynchronously copies the current file to the specified destination path.
        /// </summary>
        /// <param name="destination">The path to which the file will be copied. This must be a valid file path and cannot be null or empty.</param>
        /// <param name="overwrite">true to overwrite the destination file if it already exists; otherwise, false.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a <see cref="IFileObject"/> representing
        /// the copied file.</returns>
        Task<IFileObject> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously copies the current file to the specified destination path.
        /// </summary>
        /// <remarks>If a file already exists at the destination and overwrite is false, the operation may
        /// fail or throw an exception, depending on the implementation. Progress updates are reported on the thread
        /// that invokes the progress handler.</remarks>
        /// <param name="destination">The destination path where the file will be copied. This must be a valid path in the target storage
        /// location.</param>
        /// <param name="progress">An optional progress handler that receives progress updates during the copy operation. May be null if
        /// progress reporting is not required.</param>
        /// <param name="overwrite">true to overwrite the file at the destination if it already exists; otherwise, false.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a <see cref="IFileObject"/> representing
        /// the copied file at the destination.</returns>
        Task<IFileObject> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the storage object from the file system.
        /// </summary>
        void Delete();

        /// <summary>
        /// Asynchronously deletes the storage object from the file system.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        Task DeleteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        void Move(string destination, bool overwrite);

        /// <summary>
        /// Moves the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="progress">An optional progress reporter for move progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        void Move(string destination, IProgress<StorageProgress>? progress, bool overwrite = false);

        /// <summary>
        /// Asynchronously moves the storage object to the specified destination path, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous move operation.</returns>
        Task MoveAsync(string destination, bool overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously moves the storage object to the specified destination path with progress reporting, optionally overwriting the destination if it exists.
        /// </summary>
        /// <param name="destination">The destination path where the object will be moved.</param>
        /// <param name="progress">An optional progress reporter for move progress.</param>
        /// <param name="overwrite">true to overwrite the destination if it exists; otherwise, false.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous move operation.</returns>
        Task MoveAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renames the current object to the specified name.
        /// </summary>
        /// <param name="newName">The new name to assign to the object. Cannot be null or empty.</param>
        void Rename(string newName);

        /// <summary>
        /// Asynchronously renames the current item to the specified name.
        /// </summary>
        /// <param name="newName">The new name to assign to the item. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the rename operation.</param>
        /// <returns>A task that represents the asynchronous rename operation.</returns>
        Task RenameAsync(string newName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the specified byte array to the underlying stream or destination.
        /// </summary>
        /// <param name="content">The byte array containing the data to write. Cannot be null.</param>
        void Write(byte[] content);

        /// <summary>
        /// Writes the specified content to the storage destination, reporting progress updates as the operation
        /// proceeds.
        /// </summary>
        /// <param name="content">The byte array containing the data to write. Cannot be null.</param>
        /// <param name="progress">An object that receives progress updates during the write operation. Can be null if progress reporting is
        /// not required.</param>
        void Write(byte[] content, IProgress<StorageProgress> progress);

        /// <summary>
        /// Asynchronously writes the current object's data to the specified stream, optionally overwriting existing
        /// content.
        /// </summary>
        /// <remarks>This method is thread-safe. The underlying file operation is protected by an internal
        /// semaphore to prevent concurrent access to the same file.</remarks>
        /// <param name="stream">The target stream to which the object's data will be written. Must be writable and remain open for the
        /// duration of the operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAsync(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously writes the current object's data to the specified stream, optionally reporting progress and
        /// controlling overwrite behavior.
        /// </summary>
        /// <remarks>The write operation is thread-safe. An internal semaphore ensures that concurrent operations
        /// on the same file are properly serialized.</remarks>
        /// <param name="stream">The destination stream to which the object's data will be written. Must be writable and remain open for the
        /// duration of the operation.</param>
        /// <param name="progress">An progress reporter that receives updates about the write operation.
        /// reported.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the asynchronous write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAsync(Stream stream, IProgress<StorageProgress> progress, CancellationToken cancellationToken = default);
    }
}
