using Rheo.Storage.Handling;
using Rheo.Storage.Information;

namespace Rheo.Storage
{
    /// <summary>
    /// Represents a file in the file system and provides methods for file manipulation, including copying, moving,
    /// deleting, renaming, and reading or writing file data to streams.
    /// </summary>
    /// <remarks>FileObject instances encapsulate file operations with thread safety and support both
    /// synchronous and asynchronous workflows. The class ensures that the underlying file exists upon initialization
    /// and provides mechanisms for progress reporting and cancellation in long-running operations. FileObject is
    /// designed to be used in scenarios where robust file management and integration with streams are
    /// required.</remarks>
    public class FileObject : StorageObject<FileObject, FileInformation>
    {
        /// <summary>
        /// Initializes a new instance of the FileObject class for the specified file path, creating the file if it does
        /// not already exist.
        /// </summary>
        /// <remarks>If the specified file does not exist, it is created. The constructor does not keep
        /// the file open after initialization.</remarks>
        /// <param name="path">The path to the file to be represented by this object. Can be either an absolute or relative path.</param>
        public FileObject(string path) : base(path)
        {
            path = FullPath; // Ensure base class has processed the path

            // Ensure the file exists without holding a stream open
            File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read).Dispose();
        }

        /// <inheritdoc/>
        public override string Name
        {
            get
            {
                lock (Lock)
                {
                    return Path.GetFileName(FullPath);
                }
            }
        }

        /// <inheritdoc/>
        public override FileObject Copy(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            return FileHandling.Copy(this, destination, overwrite);
        }

        /// <inheritdoc/>
        public override FileObject Copy(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            return FileHandling.Copy(this, destination, overwrite, progress);
        }

        /// <inheritdoc/>
        public override Task<FileObject> CopyAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return FileHandling.CopyAsync(this, destination, overwrite, null, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<FileObject> CopyAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileHandling.CopyAsync(this, destination, overwrite, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Delete()
        {
            ThrowIfDisposed();
            FileHandling.Delete(this);
        }

        /// <inheritdoc/>
        public override Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileHandling.DeleteAsync(this, cancellationToken);
        }

        /// <inheritdoc/>
        public override FileObject Move(string destination, bool overwrite)
        {
            ThrowIfDisposed();
            return FileHandling.Move(this, destination, overwrite);
        }

        /// <inheritdoc/>
        public override FileObject Move(string destination, IProgress<StorageProgress>? progress, bool overwrite = false)
        {
            ThrowIfDisposed();
            return FileHandling.Move(this, destination, overwrite, progress);
        }

        /// <inheritdoc/>
        public override Task<FileObject> MoveAsync(string destination, bool overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileHandling.MoveAsync(this, destination, overwrite, null, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<FileObject> MoveAsync(string destination, IProgress<StorageProgress>? progress, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileHandling.MoveAsync(this, destination, overwrite, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Rename(string newName)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Rename already locks
            var newObj = FileHandling.Rename(this, newName);
            CopyFrom(newObj);  // CopyFrom has its own lock
            newObj.Dispose();
        }

        /// <inheritdoc/>
        public override async Task RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // ❌ REMOVE THE LOCK - FileHandling.RenameAsync already locks!
            var newObj = await FileHandling.RenameAsync(this, newName, cancellationToken);
            
            CopyFrom(newObj);   // CopyFrom has its own lock
            newObj.Dispose();
        }

        /// <summary>
        /// Writes the current object's data to the specified stream, optionally overwriting existing content.
        /// </summary>
        public void Write(Stream stream, bool overwrite = true)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Write already locks
            var newObj = FileHandling.Write(this, stream, overwrite, null);
            CopyFrom(newObj);
            newObj.Dispose();
        }

        /// <summary>
        /// Writes the current object's data to the specified stream with progress reporting.
        /// </summary>
        public void Write(Stream stream, IProgress<StorageProgress> progress, bool overwrite = true)
        {
            ThrowIfDisposed();

            // ✅ NO LOCK - FileHandling.Write already locks
            var newObj = FileHandling.Write(this, stream, overwrite, progress);
            CopyFrom(newObj);
            newObj.Dispose();
        }

        /// <summary>
        /// Asynchronously writes the current object's data to the specified stream, optionally overwriting existing
        /// content.
        /// </summary>
        /// <remarks>This method is thread-safe. The underlying file operation is protected by an internal
        /// semaphore to prevent concurrent access to the same file.</remarks>
        /// <param name="stream">The target stream to which the object's data will be written. Must be writable and remain open for the
        /// duration of the operation.</param>
        /// <param name="overwrite">Specifies whether to overwrite existing content in the stream. Set to <see langword="true"/> to overwrite;
        /// otherwise, <see langword="false"/> to append or preserve existing data.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAsync(Stream stream, bool overwrite = true, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // ❌ REMOVE THE LOCK - FileHandling.WriteAsync already locks!
            var newObj = await FileHandling.WriteAsync(this, stream, overwrite, null, cancellationToken);

            CopyFrom(newObj);   // CopyFrom has its own lock
            newObj.Dispose();
        }

        /// <summary>
        /// Asynchronously writes the current object's data to the specified stream, optionally reporting progress and
        /// controlling overwrite behavior.
        /// </summary>
        /// <remarks>The write operation is thread-safe. An internal semaphore ensures that concurrent operations
        /// on the same file are properly serialized.</remarks>
        /// <param name="stream">The destination stream to which the object's data will be written. Must be writable and remain open for the
        /// duration of the operation.</param>
        /// <param name="progress">An optional progress reporter that receives updates about the write operation. If null, no progress will be
        /// reported.</param>
        /// <param name="overwrite">A value indicating whether existing data in the destination should be overwritten. If <see
        /// langword="true"/>, any existing data will be replaced; otherwise, the operation may fail if data already
        /// exists.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the asynchronous write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAsync(Stream stream, IProgress<StorageProgress>? progress, bool overwrite = true, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // ❌ REMOVE THE LOCK - FileHandling.WriteAsync already locks!
            var newObj = await FileHandling.WriteAsync(this, stream, overwrite, progress, cancellationToken);

            CopyFrom(newObj);   // CopyFrom has its own lock
            newObj.Dispose();
        }
    }
}
