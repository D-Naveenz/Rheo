using Microsoft.Win32.SafeHandles;
using Rheo.Storage.Information;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rheo.Storage.COM
{
    [SupportedOSPlatform("windows")]
    internal static partial class Win32
    {
        #region Windows COM Types
        // Flags for SHGetFileInfo - can be combined with bitwise OR
        [Flags]
        private enum SHGFI : uint
        {
            Icon = 0x000000100,              // Retrieve icon handle
            DisplayName = 0x000000200,       // Retrieve display name
            TypeName = 0x000000400,          // Retrieve file type description
            Attributes = 0x000000800,        // Retrieve file/folder attributes
            IconLocation = 0x000001000,      // Retrieve icon file name and index
            ExeType = 0x000002000,           // Retrieve executable type
            SysIconIndex = 0x000004000,      // Retrieve system image list index
            LinkOverlay = 0x000008000,       // Add link overlay to icon
            Selected = 0x000010000,          // Show icon in selected state
            AttrSpecified = 0x000020000,     // Use specified attributes only
            LargeIcon = 0x000000000,         // Retrieve large icon
            SmallIcon = 0x000000001,         // Retrieve small icon
            OpenIcon = 0x000000002,          // Retrieve open icon
            ShellIconSize = 0x000000004,     // Retrieve shell-sized icon
            Pidl = 0x000000008,              // pszPath is PIDL, not file path
            UseFileAttributes = 0x000000010  // Use dwFileAttributes parameter
        }

        // Structure for receiving file info from SHGetFileInfo
        // Caller must manage icon handle lifetime
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct SHFILEINFO
        {
            public nint hIcon;                                  // Icon handle (must be destroyed by caller)
            public int iIcon;                                   // Icon index
            public uint dwAttributes;                           // File attributes bitmask

            // Fixed-size buffers are Native AOT friendly
            public fixed char szDisplayName[260];               // Display name (MAX_PATH = 260)
            public fixed char szTypeName[80];                   // Type description (max 80 chars)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;          // Low 32 bits of the file time
            public uint dwHighDateTime;         // High 32 bits of the file time
        }

        // File information structure from GetFileInformationByHandle
        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint dwFileAttributes;           // File attributes
            public FILETIME ftCreationTime;         // Creation time
            public FILETIME ftLastAccessTime;       // Last access time
            public FILETIME ftLastWriteTime;        // Last write time
            public uint dwVolumeSerialNumber;       // Volume serial number
            public uint nFileSizeHigh;              // High 32 bits of file size
            public uint nFileSizeLow;               // Low 32 bits of file size
            public uint nNumberOfLinks;             // Number of hard links
            public uint nFileIndexHigh;             // High 32 bits of file index
            public uint nFileIndexLow;              // Low 32 bits of file index
        }

        // Reparse point data structure for symbolic links and junctions
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;                             // Reparse point tag
            public ushort ReparseDataLength;                    // Length of reparse data
            public ushort Reserved;                             // Reserved field
            public ushort SubstituteNameOffset;                 // Offset to substitute name in PathBuffer
            public ushort SubstituteNameLength;                 // Length of substitute name
            public ushort PrintNameOffset;                      // Offset to print name in PathBuffer
            public ushort PrintNameLength;                      // Length of print name
            public uint Flags;                                  // Flags (for symbolic links)

            // Fixed-size buffers are Native AOT friendly
            public fixed byte PathBuffer[8184];                 // Buffer for link target path
        }

        #endregion

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        private const uint SE_FILE_OBJECT = 1;
        private const uint OWNER_SECURITY_INFORMATION = 0x00000001;
        private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;
        private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;

        /// <summary>
        /// Retrieves comprehensive file information for a file or directory on Windows using native Win32 APIs.
        /// </summary>
        /// <remarks>This method provides detailed file metadata including attributes, timestamps, size,
        /// security information, and Shell-provided display data. Assumes the file exists at the specified path.
        /// Icon handles must be destroyed using DestroyIcon when no longer needed.</remarks>
        /// <param name="absolutePath">The absolute path to the file or directory. Must exist.</param>
        /// <returns>A <see cref="WindowsStorageInfo"/> structure containing comprehensive file information.</returns>
        /// <exception cref="Win32Exception">Thrown if the file information cannot be retrieved.</exception>
        public static WindowsStorageInfo GetFileInformation(string absolutePath)
        {
            var info = new WindowsStorageInfo();

            // Get Shell information (icon, display name, type name)
            SHGetFileInfo(absolutePath, 0, out SHFILEINFO shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(),
                (uint)(SHGFI.Icon | SHGFI.DisplayName | SHGFI.TypeName | SHGFI.Attributes));

            info.Icon = Icon.FromHandle(shinfo.hIcon);	// Load the icon from an HICON handle
            unsafe
            {
                info.DisplayName = shinfo.szDisplayName != null
                    ? new string(shinfo.szDisplayName)
                    : string.Empty;
                info.TypeName = shinfo.szTypeName != null
                    ? new string(shinfo.szTypeName)
                    : string.Empty;
            }

            // Get detailed file information - NOW WITH AUTOMATIC DISPOSAL
            using SafeFileHandle fileHandle = CreateFile(absolutePath, GENERIC_READ, FILE_SHARE_READ,
                nint.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nint.Zero);

            if (fileHandle.IsInvalid)
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw new Win32Exception(errorCode, $"Failed to open file handle for '{absolutePath}'.");
            }

            // Get file information by handle
            if (!GetFileInformationByHandle(fileHandle, out BY_HANDLE_FILE_INFORMATION fileInfo))
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw new Win32Exception(errorCode, $"Failed to get file information for '{absolutePath}'.");
            }

            info.Attributes = (FileAttributes)fileInfo.dwFileAttributes;
            info.Size = ((ulong)fileInfo.nFileSizeHigh << 32) | fileInfo.nFileSizeLow;
            info.CreationTime = FileTimeToDateTime(fileInfo.ftCreationTime);
            info.LastAccessTime = FileTimeToDateTime(fileInfo.ftLastAccessTime);
            info.LastWriteTime = FileTimeToDateTime(fileInfo.ftLastWriteTime);
            info.VolumeSerialNumber = fileInfo.dwVolumeSerialNumber;
            info.HardLinkCount = fileInfo.nNumberOfLinks;
            info.FileIndex = ((ulong)fileInfo.nFileIndexHigh << 32) | fileInfo.nFileIndexLow;

            // Get owner SID
            if (GetSecurityInfo(fileHandle, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION,
                out nint pOwnerSid, out _, out _, out _, out nint pSecurityDescriptor))
            {
                if (ConvertSidToStringSid(pOwnerSid, out nint pStringSid))
                {
                    info.OwnerSid = Marshal.PtrToStringUni(pStringSid);
                    LocalFree(pStringSid);
                }
                LocalFree(pSecurityDescriptor);
            }

            // Get reparse point target if applicable
            if ((fileInfo.dwFileAttributes & (uint)FileAttributes.ReparsePoint) != 0)
            {
                info.ReparseTarget = GetReparsePointTarget(fileHandle);
            }

            // Always destroy the icon handle if SHGFI_ICON was used
            if (shinfo.hIcon != nint.Zero)
            {
                DestroyIcon(shinfo.hIcon);
            }

            return info;
        }

        /// <summary>
        /// Retrieves information about a file or folder, such as its icon, display name, and type, from the Windows
        /// Shell.
        /// </summary>
        /// <remarks>This method is a platform invocation of the Windows Shell SHGetFileInfo function and
        /// is supported only on Windows. The meaning of the return value and the contents of the <paramref
        /// name="psfi"/> structure depend on the combination of flags specified in <paramref name="uFlags"/>. Callers
        /// are responsible for managing any resources returned, such as destroying icon handles when no longer needed.
        /// For more information about valid flags and usage, see the Windows API documentation for
        /// SHGetFileInfo.</remarks>
        /// <param name="pszPath">The path to the file or folder for which to retrieve information. This can be a fully qualified path or,
        /// depending on the flags specified in <paramref name="uFlags"/>, a special folder identifier. Can be <see
        /// langword="null"/> if <paramref name="uFlags"/> specifies a value that does not require a path.</param>
        /// <param name="dwFileAttributes">The file attribute flags to use when retrieving information. These are typically standard file attribute
        /// constants (such as FILE_ATTRIBUTE_DIRECTORY). If <paramref name="uFlags"/> includes the
        /// SHGFI_USEFILEATTRIBUTES flag, this parameter specifies the attributes to use; otherwise, it is ignored.</param>
        /// <param name="psfi">When this method returns, contains a <see cref="SHFILEINFO"/> structure that receives the file information
        /// retrieved by the function.</param>
        /// <param name="cbFileInfo">The size, in bytes, of the <see cref="SHFILEINFO"/> structure pointed to by <paramref name="psfi"/>. This
        /// value must be set to <c>sizeof(SHFILEINFO)</c>.</param>
        /// <param name="uFlags">A combination of SHGFI_* flags that specify which file information to retrieve. These flags control the type
        /// of information returned, such as icons, display names, or type names.</param>
        /// <returns>If successful, returns a nonzero value that depends on the flags specified in <paramref name="uFlags"/> (for
        /// example, a handle to an icon or a system image list index). Returns zero if the function fails.</returns>
        [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        /// <summary>
        /// Destroys an icon and frees any associated system resources.
        /// </summary>
        /// <remarks>Call this method to release system resources when an icon is no longer needed. After
        /// calling this method, the icon handle is no longer valid and should not be used. This method is supported
        /// only on Windows platforms.</remarks>
        /// <param name="hIcon">A handle to the icon to be destroyed. This handle must have been obtained from a previous call to a function
        /// that creates or loads icons. Passing an invalid or already destroyed handle may result in undefined
        /// behavior.</param>
        /// <returns>true if the function succeeds; otherwise, false.</returns>
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyIcon(nint hIcon);

        /// <summary>
        /// Creates or opens a file or I/O device and returns a handle that can be used to access it. This method
        /// provides access to files, directories, physical disks, volumes, consoles, and other devices supported by the
        /// Windows API.
        /// </summary>
        /// <remarks>This method is a direct P/Invoke signature for the Windows API CreateFile function.
        /// The caller is responsible for closing the returned handle using the appropriate method (such as CloseHandle)
        /// to avoid resource leaks. If the method fails, use GetLastError to retrieve the error code. Thread safety and
        /// access rights depend on the parameters provided and the underlying system configuration.</remarks>
        /// <param name="lpFileName">The name or path of the file or device to be created or opened. This can be a relative or absolute path.
        /// Cannot be null.</param>
        /// <param name="dwDesiredAccess">The requested access to the file or device, specified as a combination of access flags (such as read, write,
        /// or both).</param>
        /// <param name="dwShareMode">The sharing mode for the file or device, determining how the file can be shared with other processes.
        /// Specify flags to allow read, write, or delete sharing.</param>
        /// <param name="lpSecurityAttributes">A pointer to a SECURITY_ATTRIBUTES structure that determines whether the returned handle can be inherited by
        /// child processes. Can be zero for default security.</param>
        /// <param name="dwCreationDisposition">An action to take on files that exist or do not exist, such as creating a new file, opening an existing
        /// file, or overwriting an existing file. Specify one of the creation disposition flags.</param>
        /// <param name="dwFlagsAndAttributes">File or device attributes and flags, such as whether the file is hidden, read-only, or supports asynchronous
        /// operations.</param>
        /// <param name="hTemplateFile">A handle to a template file with the desired attributes to copy to the new file, or zero if not used. Only
        /// used when creating a new file.</param>
        /// <returns>A handle to the created or opened file or device. Returns an invalid handle value if the operation fails.</returns>
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.safehandles.safefilehandle?view=net-9.0
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

        /// <summary>
        /// Retrieves information about the specified file using its handle.
        /// </summary>
        /// <remarks>This method is a platform invocation of the Windows API function
        /// GetFileInformationByHandle. It is supported only on Windows. The caller is responsible for ensuring that the
        /// file handle remains valid for the duration of the call.</remarks>
        /// <param name="hFile">A handle to the file for which information is to be retrieved. The handle must have been created with
        /// appropriate access rights.</param>
        /// <param name="lpFileInformation">When this method returns, contains a structure that receives the file information. This parameter is passed
        /// uninitialized.</param>
        /// <returns>true if the function succeeds; otherwise, false. To get extended error information, call GetLastError.</returns>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        /// <summary>
        /// Sends a control code directly to a specified device driver, causing the corresponding device to perform an
        /// operation defined by the control code.
        /// </summary>
        /// <remarks>If the method returns false, call GetLastError to obtain extended error information.
        /// The behavior and requirements of each parameter depend on the specific control code used. This method is
        /// typically used for advanced device management and may require elevated privileges.</remarks>
        /// <param name="hDevice">A handle to the device on which the operation is to be performed. This handle must have been obtained using
        /// a method such as CreateFile and must have appropriate access rights for the requested operation.</param>
        /// <param name="dwIoControlCode">The control code that specifies the operation to be performed. The value must be a valid device-specific or
        /// system-defined I/O control code.</param>
        /// <param name="lpInBuffer">A pointer to a buffer that contains input data required for the operation, or <see langword="null"/> if no
        /// input data is needed.</param>
        /// <param name="nInBufferSize">The size, in bytes, of the input buffer pointed to by <paramref name="lpInBuffer"/>. Set to zero if no input
        /// buffer is provided.</param>
        /// <param name="lpOutBuffer">When the method returns, contains the output data produced by the device driver. The structure and content
        /// depend on the control code specified.</param>
        /// <param name="nOutBufferSize">The size, in bytes, of the output buffer that receives data from the device driver.</param>
        /// <param name="lpBytesReturned">When the method returns, contains the number of bytes returned in the output buffer.</param>
        /// <param name="lpOverlapped">A pointer to an OVERLAPPED structure for asynchronous operations, or <see langword="null"/> for synchronous
        /// operation.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, nint lpInBuffer, uint nInBufferSize, out REPARSE_DATA_BUFFER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, nint lpOverlapped);

        /// <summary>
        /// Retrieves security information about a specified object, including owner, group, discretionary access
        /// control list (DACL), system access control list (SACL), and security descriptor.
        /// </summary>
        /// <remarks>This method is a P/Invoke signature for the native GetSecurityInfo function in
        /// advapi32.dll. If the method returns false, call Marshal.GetLastWin32Error to obtain the error code. The
        /// caller must free any memory allocated for output pointers using LocalFree. This method is intended for
        /// advanced scenarios involving Windows security APIs.</remarks>
        /// <param name="handle">A handle to the object from which to retrieve security information. The handle must have appropriate access
        /// rights depending on the requested security information.</param>
        /// <param name="objectType">The type of the object referenced by the handle. This value specifies the kind of object (such as file,
        /// service, or registry key) for which security information is being requested.</param>
        /// <param name="securityInfo">A bitmask that specifies the security information to retrieve, such as owner, group, DACL, or SACL. The
        /// value determines which output parameters will be populated.</param>
        /// <param name="ppsidOwner">When the owner information is requested, receives a pointer to the security identifier (SID) of the object's
        /// owner.</param>
        /// <param name="ppsidGroup">When the group information is requested, receives a pointer to the security identifier (SID) of the object's
        /// primary group.</param>
        /// <param name="ppDacl">When DACL information is requested, receives a pointer to the object's discretionary access control list.</param>
        /// <param name="ppSacl">When SACL information is requested, receives a pointer to the object's system access control list.</param>
        /// <param name="ppSecurityDescriptor">Receives a pointer to the security descriptor structure for the object. The caller is responsible for
        /// freeing this memory using the appropriate API.</param>
        /// <returns>true if the security information was retrieved successfully; otherwise, false.</returns>
        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInfo, out nint ppsidOwner, out nint ppsidGroup, out nint ppDacl, out nint ppSacl, out nint ppSecurityDescriptor);

        /// <summary>
        /// Converts a security identifier (SID) to its string representation.
        /// </summary>
        /// <remarks>This method is a P/Invoke signature for the Windows API function
        /// ConvertSidToStringSid. If the method returns false, call Marshal.GetLastWin32Error to retrieve the error
        /// code. The memory pointed to by pStringSid must be freed using LocalFree to avoid memory leaks.</remarks>
        /// <param name="pSid">A pointer to the SID structure to be converted. Must reference a valid SID.</param>
        /// <param name="pStringSid">When this method returns, contains a pointer to a null-terminated Unicode string that represents the SID.
        /// The caller is responsible for freeing this memory using LocalFree.</param>
        /// <returns>true if the conversion succeeds; otherwise, false.</returns>
        [LibraryImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ConvertSidToStringSid(nint pSid, out nint pStringSid);

        /// <summary>
        /// Frees the memory allocated by the local memory management functions.
        /// </summary>
        /// <remarks>After the memory is freed, the handle is no longer valid and must not be used. This
        /// method should only be used with memory allocated by local memory functions such as LocalAlloc. Passing an
        /// invalid or already freed handle may result in undefined behavior.</remarks>
        /// <param name="hMem">A handle to the local memory object to be freed. This handle must have been returned by a previous call to a
        /// local memory allocation function and must not be null.</param>
        /// <returns>If the function succeeds, the return value is zero. If the function fails, the return value is equal to the
        /// input handle.</returns>
        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", EntryPoint = "LocalFreeW")]
        private static partial nint LocalFree(nint hMem);

        private static DateTime FileTimeToDateTime(FILETIME fileTime)
        {
            long fileTimeValue = ((long)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
            return DateTime.FromFileTimeUtc(fileTimeValue);
        }

        private static string? GetReparsePointTarget(SafeFileHandle fileHandle)
        {
            if (!DeviceIoControl(fileHandle, FSCTL_GET_REPARSE_POINT, nint.Zero, 0,
                out REPARSE_DATA_BUFFER reparseData, (uint)Marshal.SizeOf<REPARSE_DATA_BUFFER>(), out _, nint.Zero))
            {
                return null;
            }

            if (reparseData.ReparseTag == IO_REPARSE_TAG_SYMLINK)
            {
                int offset = reparseData.PrintNameOffset / 2;
                int length = reparseData.PrintNameLength / 2;
                unsafe
                {
                    // Fix: Use unsafe context and Marshal.Copy to copy from byte* to byte[]
                    byte[] buffer = new byte[length * 2];
                    Marshal.Copy((nint)(reparseData.PathBuffer + offset * 2), buffer, 0, length * 2);
                    return System.Text.Encoding.Unicode.GetString(buffer);
                }
            }

            return null;
        }
    }
}
