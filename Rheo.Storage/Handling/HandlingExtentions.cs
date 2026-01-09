namespace Rheo.Storage.Handling
{
    internal static class HandlingExtentions
    {
        private static readonly Dictionary<string, SemaphoreSlim> _handlingLocks = [];
        private static readonly Lock _dictionaryLock = new();

        /// <summary>
        /// Retrieves a semaphore used to synchronize access to the specified storage object.
        /// </summary>
        /// <remarks>The returned semaphore is unique per storage object's full path. Use this lock to
        /// ensure thread-safe operations on the same storage object across multiple threads.</remarks>
        /// <param name="source">The storage object for which to obtain the handling lock. Cannot be null.</param>
        /// <returns>A <see cref="SemaphoreSlim"/> instance that can be used to coordinate access to the specified storage
        /// object.</returns>
        public static SemaphoreSlim GetHandlingLock(this StorageObject source)
        {
            var path = source.FullPath;

            lock (_dictionaryLock)
            {
                if (!_handlingLocks.TryGetValue(path, out var semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    _handlingLocks[path] = semaphore;
                }
                return semaphore;
            }
        }

        /// <summary>
        /// Determines whether the source storage object and the specified destination path reside on the same root
        /// volume or drive.
        /// </summary>
        /// <remarks>This method compares the root components (such as drive letters on Windows) of the
        /// full paths. If either path is invalid or inaccessible, the method returns false.</remarks>
        /// <param name="source">The storage object whose root path is to be compared.</param>
        /// <param name="destpath">The destination file or directory path to compare with the source object's root. Cannot be null.</param>
        /// <returns>true if both the source object's path and the destination path share the same root volume or drive;
        /// otherwise, false.</returns>
        public static bool IsInTheSameRoot(this StorageObject source, string destpath)
        {
            try
            {
                var drive1 = Path.GetPathRoot(Path.GetFullPath(source.FullPath));
                var drive2 = Path.GetPathRoot(Path.GetFullPath(destpath));

                return string.Equals(drive1, drive2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If we can't determine, assume different volumes
                return false;
            }
        }
    }
}
