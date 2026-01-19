using Rheo.Storage.Information;

namespace Rheo.Storage.Contracts
{
    /// <summary>
    /// Represents a directory object in a storage system, providing operations for copying, moving, deleting, and
    /// renaming directories, as well as access to directory-specific information.
    /// </summary>
    /// <remarks>IDirectoryObject extends <see cref="IStorageObject"/> to support directory-specific operations and
    /// metadata. Implementations may provide both synchronous and asynchronous methods for file system manipulation,
    /// including support for progress reporting and cancellation. Thread safety and atomicity of operations are not
    /// guaranteed and may depend on the underlying storage provider.</remarks>
    public interface IDirectoryObject : IStorageObject
    {
        /// <summary>
        /// Gets the directory-specific information associated with this instance.
        /// </summary>
        /// <remarks>This property returns the underlying information as a DirectoryInformation object. If the
        /// base information is not of type DirectoryInformation, an InvalidOperationException is thrown.</remarks>
        new DirectoryInformation Information { get; }

        #region Getters
        /// <summary>
        /// Retrieves the file names from the directory represented by this instance, based on the specified search
        /// pattern and search option.
        /// </summary>
        /// <remarks>This method uses the <see cref="Directory.GetFiles(string, string, SearchOption)"/>
        /// method internally to retrieve the file names.</remarks>
        /// <param name="searchPattern">The search string to match against the names of files in the directory. The default value is "*", which
        /// matches all files.</param>
        /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories. The default value is <see
        /// cref="SearchOption.TopDirectoryOnly"/>.</param>
        /// <returns>An array of strings containing the full paths of the files that match the specified
        /// search pattern and search option.</returns>
        /// <exception cref="IOException">Thrown if an I/O error occurs while accessing the directory or its contents.</exception>
        string[] GetFiles(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Retrieves a file from the specified relative path within the current directory.
        /// </summary>
        /// <remarks>The method combines the provided relative path with the current directory's full path
        /// to locate the file. Ensure that the relative path is valid and points to an existing file within the
        /// directory.</remarks>
        /// <param name="relativePath">The relative path to the file, starting from the current directory. The path must not be rooted.</param>
        /// <returns>A <see cref="IFileObject"/> instance representing the file at the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is an absolute path.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist at the specified relative path.</exception>
        IFileObject GetFile(string relativePath);

        /// <summary>
        /// Retrieves the names of subdirectories in the current directory that match the specified search pattern and
        /// search option.
        /// </summary>
        /// <remarks>This method wraps <see cref="Directory.GetDirectories(string, string,
        /// SearchOption)"/> and provides additional context in the exception message if an error occurs.</remarks>
        /// <param name="searchPattern">The search string to match against the names of subdirectories. The default value is "*", which matches all
        /// subdirectories.</param>
        /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories. The default value is <see
        /// cref="SearchOption.TopDirectoryOnly"/>.</param>
        /// <returns>An array of the full paths of subdirectories that match the specified search pattern and
        /// search option.</returns>
        /// <exception cref="IOException">Thrown if an I/O error occurs while accessing the file system, or if an error occurs while retrieving
        /// directories.</exception>
        string[] GetDirectories(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Retrieves a <see cref="DirectoryObject"/> representing the directory at the specified relative path.
        /// </summary>
        /// <remarks>This method combines the specified <paramref name="relativePath"/> with the base
        /// directory's full path to locate the target directory. Ensure that the relative path is valid and points to
        /// an existing directory.</remarks>
        /// <param name="relativePath">The relative path to the directory, relative to the base directory represented by this instance. The path
        /// must not be rooted.</param>
        /// <returns>A <see cref="IDirectoryObject"/> representing the directory at the specified relative path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="relativePath"/> is a rooted path.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by <paramref name="relativePath"/> does not exist.</exception>
        IDirectoryObject GetDirectory(string relativePath);
        #endregion

        #region Handlers
        /// <summary>
        /// Creates a copy of the current directory object at the specified destination path.
        /// </summary>
        /// <param name="destination">The path where the directory object will be copied. This must be a valid path for the target location.</param>
        /// <param name="overwrite">true to overwrite the destination if it already exists; otherwise, false.</param>
        /// <returns>A <see cref="IDirectoryObject"/> representing the newly copied directory at the destination path.</returns>
        IDirectoryObject Copy(string destination, bool overwrite);

        /// <summary>
        /// Creates a copy of the current directory object at the specified destination path.
        /// </summary>
        /// <remarks>If the destination already exists and overwrite is set to false, the method may throw
        /// an exception or fail to copy, depending on the implementation. The copy operation may be recursive and can
        /// include all subdirectories and files.</remarks>
        /// <param name="destination">The path where the directory object will be copied. This must be a valid destination path and cannot be null
        /// or empty.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the copy operation. May be null if progress
        /// reporting is not required.</param>
        /// <param name="overwrite">true to overwrite the destination if it already exists; otherwise, false.</param>
        /// <returns>A new <see cref="IDirectoryObject"/> representing the copied directory at the destination path.</returns>
        IDirectoryObject Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false);

        /// <summary>
        /// Asynchronously creates a copy of the current directory at the specified destination path.
        /// </summary>
        /// <param name="destination">The path where the directory will be copied. This must be a valid directory path.</param>
        /// <param name="overwrite">true to overwrite the destination directory if it already exists; otherwise, false.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a <see cref="IDirectoryObject"/>
        /// representing the copied directory.</returns>
        Task<IDirectoryObject> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously creates a copy of the current directory object at the specified destination path.
        /// </summary>
        /// <remarks>If the destination already exists and overwrite is set to false, the operation may
        /// fail. The method does not guarantee atomicity; partial copies may exist if the operation is
        /// interrupted.</remarks>
        /// <param name="destination">The destination path where the directory object will be copied. This must be a valid path and cannot be null
        /// or empty.</param>
        /// <param name="progress">An optional progress handler that receives progress updates during the copy operation. May be null if
        /// progress reporting is not required.</param>
        /// <param name="overwrite">true to overwrite the destination if it already exists; otherwise, false.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
        /// <returns>A task that represents the asynchronous copy operation. The task result contains a <see cref="IDirectoryObject"/>
        /// representing the newly created copy at the destination.</returns>
        Task<IDirectoryObject> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default);

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
        #endregion
    }
}
