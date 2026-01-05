using Rheo.Storage.Contracts;
using System.Drawing;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides an abstract base for accessing and representing platform-specific information about a file or storage
    /// item, including attributes, size, timestamps, and ownership details.
    /// </summary>
    /// <remarks>This class exposes a unified interface for retrieving storage metadata across different
    /// operating systems. It supports properties for common file attributes, symbolic link information, and
    /// platform-specific details such as owner identifiers and access modes. Implementations should provide the actual
    /// logic for retrieving the file size. Thread safety is ensured for property access. Use derived types to access
    /// additional platform-specific features as needed.</remarks>
    public abstract class StorageInformation : IStorageInformation
    {
        private readonly TaskCompletionSource<IStorageInfoStruct> _storageInfoTaskAwaiter;

        /// <summary>
        /// Represents the absolute path associated with the current instance.
        /// </summary>
        /// <remarks>This field is intended for use by derived classes that require access to the absolute
        /// path value. The value should be a fully qualified path and may be used for file system operations or
        /// resource identification.</remarks>
        protected readonly string _absPath;
        /// <summary>
        /// Lazily retrieves the platform-specific storage information structure for the current storage item.
        /// </summary>
        protected readonly Lazy<IStorageInfoStruct> _storageInfoLazy;

        /// <summary>
        /// Initializes a new instance of the StorageInformation class for the specified absolute file path.
        /// </summary>
        /// <remarks>This constructor begins retrieving platform-specific storage information in the
        /// background upon initialization. Accessing storage information properties may block until retrieval is
        /// complete.</remarks>
        /// <param name="absolutePath">The absolute path to the file or directory for which storage information will be retrieved. Cannot be null,
        /// empty, or consist only of white-space characters.</param>
        /// <exception cref="ArgumentException">Thrown if absolutePath is null, empty, or consists only of white-space characters.</exception>
        public StorageInformation(string absolutePath)
        {
            // Validate the path
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException("The file path cannot be null or whitespace.", nameof(absolutePath));
            }

            _absPath = absolutePath;

            // Initialize the storage info task completion source
            _storageInfoTaskAwaiter = new TaskCompletionSource<IStorageInfoStruct>();
            // Start retrieving platform-specific storage info in a background task
            Task.Run(() =>
            {
                try
                {
                    var infoStruct = GetPlatformStorageInfo();
                    _storageInfoTaskAwaiter.SetResult(infoStruct);
                }
                catch (Exception ex)
                {
                    _storageInfoTaskAwaiter.SetException(ex);
                }
            });
            _storageInfoLazy = new Lazy<IStorageInfoStruct>(() => _storageInfoTaskAwaiter.Task.GetAwaiter().GetResult());
        }

        #region Properties: Core Identity
        /// <inheritdoc/>
        public string DisplayName
        {
            get
            {
                if (TryGetWindowsStorageInfo(out var winfo))
                {
                    return winfo.DisplayName;
                }

                return Path.GetFileNameWithoutExtension(_absPath);
            }
        }

        #endregion

        #region Properties: Attributes (FileAttributes enum)
        /// <inheritdoc/>
        public FileAttributes Attributes => _storageInfoLazy.Value.Attributes;

        /// <inheritdoc/>
        public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

        /// <inheritdoc/>
        public bool IsHidden => Attributes.HasFlag(FileAttributes.Hidden);

        /// <inheritdoc/>
        public bool IsSystem => Attributes.HasFlag(FileAttributes.System);

        /// <inheritdoc/>
        public bool IsTemporary => Attributes.HasFlag(FileAttributes.Temporary);

        #endregion

        #region Properties: Size
        /// <inheritdoc/>
        public abstract ulong Size { get; }

        /// <inheritdoc/>
        public string FormattedSize => GetSizeString();

        #endregion

        #region Properties: Timestamps
        /// <inheritdoc/>
        public DateTime CreationTime => _storageInfoLazy.Value.CreationTime;

        /// <inheritdoc/>
        public DateTime LastWriteTime => _storageInfoLazy.Value.LastWriteTime;

        /// <inheritdoc/>
        public DateTime LastAccessTime => _storageInfoLazy.Value.LastAccessTime;

        #endregion

        #region Properties: Links & Targets
        /// <inheritdoc/>
        public bool IsSymbolicLink => !string.IsNullOrEmpty(GetSymbolicLinkTarget());

        /// <inheritdoc/>
        public string? LinkTarget => GetSymbolicLinkTarget();

        #endregion

        #region Properties: Platform-Specific (optional/nullable)
        /// <inheritdoc/>
        public string? OwnerSid => TryGetWindowsStorageInfo(out var winfo) ? winfo.OwnerSid : null;

        /// <inheritdoc/>
        public Icon? Icon => TryGetWindowsStorageInfo(out var winfo) ? winfo.Icon : null;

        /// <inheritdoc/>
        public uint? OwnerId => TryGetUnixStorageInfo(out var uinfo) ? uinfo.OwnerId : null;

        /// <inheritdoc/>
        public uint? GroupId => TryGetUnixStorageInfo(out var uinfo) ? uinfo.GroupId : null;

        /// <inheritdoc/>
        public uint? Mode => TryGetUnixStorageInfo(out var uinfo) ? uinfo.Mode : null;

        #endregion

        /// <inheritdoc/>
        public string GetSizeString(UOM? uom = null)
        {
            if (uom.HasValue)
            {
                var size = Size;
                return $"{size} {uom.Value}";
            }
            else
            {
                // Auto-select UOM
                var size = Size;

                return size switch
                {
                    < 1024 => $"{size} B",
                    < 1024 * 1024 => $"{size / 1024.0:F2} KB",
                    < 1024 * 1024 * 1024 => $"{size / (1024.0 * 1024):F2} MB",
                    _ => $"{size / (1024.0 * 1024 * 1024):F2} GB",
                };
            }
        }

        /// <summary>
        /// Attempts to retrieve storage information specific to Windows systems.
        /// </summary>
        /// <remarks>This method does not throw exceptions. If the underlying storage information is not
        /// compatible with Windows or an error occurs, the method returns false and <paramref name="winfo"/> is set to
        /// its default value.</remarks>
        /// <param name="windowsInfo">When this method returns, contains a <see cref="WindowsStorageInfo"/> object with Windows-specific storage
        /// details if available; otherwise, the default value.</param>
        /// <returns>true if Windows storage information was successfully retrieved; otherwise, false.</returns>
        protected bool TryGetWindowsStorageInfo(out WindowsStorageInfo windowsInfo)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    windowsInfo = InformationProvider.GetWindowsFileInfo(_absPath);
                    return true;
                }
            }
            catch
            {
                // Handle exception
            }

            windowsInfo = default;
            return false;
        }

        /// <summary>
        /// Attempts to retrieve storage information specific to Unix-based systems.
        /// </summary>
        /// <remarks>This method does not throw exceptions. If the underlying storage information is not
        /// compatible with Unix systems or an error occurs during retrieval, the method returns <see langword="false"/>
        /// and <paramref name="uinfo"/> is set to its default value.</remarks>
        /// <param name="uinfo">When this method returns, contains a <see cref="UnixStorageInfo"/> structure with the Unix storage
        /// information if available; otherwise, the default value.</param>
        /// <returns><see langword="true"/> if Unix storage information is available and was retrieved successfully; otherwise,
        /// <see langword="false"/>.</returns>
        protected bool TryGetUnixStorageInfo(out UnixStorageInfo uinfo)
        {
            uinfo = default;
            try
            {
                var storageInfo = _storageInfoLazy.Value;
                if (storageInfo is UnixStorageInfo unixInfo)
                {
                    uinfo = unixInfo;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private IStorageInfoStruct GetPlatformStorageInfo()
        {
            try
            {
                IStorageInfoStruct infoStruct;

                if (OperatingSystem.IsWindows())
                {
                    infoStruct = InformationProvider.GetWindowsFileInfo(_absPath);
                }
                else if (OperatingSystem.IsLinux())
                {
                    infoStruct = InformationProvider.GetLinuxFileInfo(_absPath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    infoStruct = InformationProvider.GetMacFileInfo(_absPath);
                }
                else
                {
                    throw new PlatformNotSupportedException("The current operating system is not supported.");
                }

                return infoStruct;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to retrieve platform-specific storage information.", ex);
            }
        }

        private string? GetSymbolicLinkTarget()
        {
            // Platform-specific implementation to get symbolic link target
            if (OperatingSystem.IsWindows())
            {
                // Windows-specific logic
                return TryGetWindowsStorageInfo(out var winfo) ? winfo.ReparseTarget : null;
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Unix-like systems logic
                return TryGetUnixStorageInfo(out var uinfo) ? uinfo.SymlinkTarget : null;
            }
            else
            {
                throw new PlatformNotSupportedException("The current operating system is not supported for symbolic link retrieval.");
            }
        }
    }

    /// <summary>
    /// Represents units of measurement for data storage sizes.
    /// </summary>
    public enum UOM
    {
        /// <summary>
        /// Bytes unit of measurement.
        /// </summary>
        Bytes,
        /// <summary>
        /// Kilobytes unit of measurement.
        /// </summary>
        KB,
        /// <summary>
        /// Megabytes unit of measurement.
        /// </summary>
        MB,
        /// <summary>
        /// Gigabytes unit of measurement.
        /// </summary>
        GB,
        /// <summary>
        /// Terabytes unit of measurement.
        /// </summary>
        TB
    }
}
