using System.Runtime.InteropServices;

namespace Rheo.Storage.Info
{
    public abstract class StorageInfomation
    {
        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

        private const uint SHGFI_ICON = 0x000000100;     // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
        private const uint SHGFI_TYPENAME = 0x000000400;     // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
        private const uint SHGFI_OPENICON = 0x000000002;     // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute

        private readonly string fullPath;

        /// <summary>
        /// Represents information about a file or folder, including its icon, display name, and type name.
        /// </summary>
        /// <remarks>This structure is used to retrieve file or folder metadata, such as the associated
        /// icon handle,  display name, and type description. It is commonly used with the SHGetFileInfo function in the
        /// Windows Shell API to obtain information about a file system object.</remarks>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public nint hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public StorageInfomation(string fullPath)
        {
            this.fullPath = fullPath;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var shinfo = GetWindowsShellFileInfo();
                    // Set file attributes based on retrieved information
                    AttributeFlags = (FileAttributes)shinfo.dwAttributes;
                    // Set type name
                    TypeName = shinfo.szTypeName;
                }
                else
                {
                    AttributeFlags = File.GetAttributes(fullPath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while accessing the file or directory.", ex);
            }

            DisplayName = Path.GetFileNameWithoutExtension(fullPath);
        }

        /// <summary>
        /// The user-friendly name for the storage. This is typically the name shown in file explorer.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// The user-friendly description of the type. This is typically a description of the storage type (e.g., "Text Document").
        /// </summary>
        public string? TypeName { get; init; }

        public FileAttributes AttributeFlags { get; init; }

        /// <summary>
        /// The MIME (Multipurpose Internet Mail Extensions) type of the storage. For example, a music file might have the "audio/mpeg" MIME type.
        /// </summary>
        public abstract string MimeType { get; }

        /// <summary>
        /// Retrieves information about an object in the file system, such as a file, folder, directory, or drive.
        /// </summary>
        /// <remarks>This method is a P/Invoke wrapper for the SHGetFileInfo function in the Windows Shell
        /// API. It is used to retrieve file system object information, such as icons, display names, and type
        /// names.</remarks>
        /// <param name="pszPath">The path to the file or folder. This can be a fully qualified path or a relative path.</param>
        /// <param name="dwFileAttributes">A combination of file attribute flags that specify the type of file information to retrieve. Set to 0 if no
        /// attributes are specified.</param>
        /// <param name="psfi">When the method returns, contains a <see cref="SHFILEINFO"/> structure that receives the file information.</param>
        /// <param name="cbFileInfo">The size, in bytes, of the <see cref="SHFILEINFO"/> structure.</param>
        /// <param name="flags">A combination of flags that specify the type of information to retrieve. For example, use SHGFI_ICON to
        /// retrieve the file's icon.</param>
        /// <returns>An <see cref="nint"/> that can be used to access the retrieved information. Returns <see
        /// langword="IntPtr.Zero"/> if the operation fails.</returns>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            uint flags);

        private SHFILEINFO GetWindowsShellFileInfo()
        {
            nint result = SHGetFileInfo(
                fullPath, 
                FILE_ATTRIBUTE_READONLY | FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM | FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_TEMPORARY, 
                out SHFILEINFO shinfo, 
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_DISPLAYNAME | SHGFI_TYPENAME | SHGFI_ATTRIBUTES | SHGFI_USEFILEATTRIBUTES);
            if (result == nint.Zero)
            {
                throw new InvalidOperationException("Failed to retrieve file information.");
            }
            return shinfo;
        }
    }
}
