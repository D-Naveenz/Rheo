using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides platform-specific methods and types for retrieving detailed file and directory information on Windows,
    /// Linux, and macOS systems.
    /// </summary>
    /// <remarks>The InfomationProvider class exposes static methods for obtaining comprehensive file
    /// metadata, including attributes, timestamps, ownership, and symbolic link information, using native system APIs.
    /// It supports Windows (via Win32 and Shell interop), Linux (via lstat and readlink), and macOS (via stat/lstat
    /// system calls), abstracting platform differences for cross-platform file inspection. Methods may throw exceptions
    /// if the specified file or directory does not exist or if native API calls fail. Callers are responsible for
    /// managing any native resources, such as icon handles, returned by certain Windows methods.</remarks>
    public static partial class InformationProvider
    {
        #region Windows Implementation
        /// <summary>
        /// Specifies flags that control the information retrieved by the Windows Shell API function SHGetFileInfo.
        /// </summary>
        /// <remarks>
        /// The <see cref="SHGFI"/> enumeration defines constants used to specify which attributes or information
        /// about a file or folder should be retrieved from the Windows Shell. These flags can be combined using a bitwise OR operation.
        /// </remarks>
        [Flags]
        public enum SHGFI : uint
        {
            /// <summary>
            /// Retrieve the handle to the icon that represents the file or folder.
            /// </summary>
            Icon = 0x000000100,

            /// <summary>
            /// Retrieve the display name for the file or folder.
            /// </summary>
            DisplayName = 0x000000200,

            /// <summary>
            /// Retrieve the string that describes the file's type.
            /// </summary>
            TypeName = 0x000000400,

            /// <summary>
            /// Retrieve the file or folder attributes.
            /// </summary>
            Attributes = 0x000000800,

            /// <summary>
            /// Retrieve the name of the file that contains the icon representing the file or folder, as well as the icon index within that file.
            /// </summary>
            IconLocation = 0x000001000,

            /// <summary>
            /// Retrieve the type of the executable file if the file is an executable.
            /// </summary>
            ExeType = 0x000002000,

            /// <summary>
            /// Retrieve the index of the icon within the system image list.
            /// </summary>
            SysIconIndex = 0x000004000,

            /// <summary>
            /// Add the link overlay to the icon, indicating that the file or folder is a shortcut.
            /// </summary>
            LinkOverlay = 0x000008000,

            /// <summary>
            /// Show the icon in the selected state.
            /// </summary>
            Selected = 0x000010000,

            /// <summary>
            /// Retrieve only the attributes that are specified in the dwFileAttributes parameter.
            /// </summary>
            AttrSpecified = 0x000020000,

            /// <summary>
            /// Retrieve the large version of the icon.
            /// </summary>
            LargeIcon = 0x000000000,

            /// <summary>
            /// Retrieve the small version of the icon.
            /// </summary>
            SmallIcon = 0x000000001,

            /// <summary>
            /// Retrieve the open version of the icon.
            /// </summary>
            OpenIcon = 0x000000002,

            /// <summary>
            /// Retrieve the shell-sized icon.
            /// </summary>
            ShellIconSize = 0x000000004,

            /// <summary>
            /// Indicates that the pszPath parameter is a pointer to an item identifier list (PIDL) rather than a file path.
            /// </summary>
            Pidl = 0x000000008,

            /// <summary>
            /// Indicates that the function should use the file attributes passed in the dwFileAttributes parameter.
            /// </summary>
            UseFileAttributes = 0x000000010
        }

        /// <summary>
        /// Contains information about a file object, including its icon, display name, type name, and attributes, for
        /// use with Windows Shell API functions.
        /// </summary>
        /// <remarks>The SHFILEINFO structure is primarily used in interop scenarios to receive
        /// information from native Windows Shell functions, such as SHGetFileInfo. Its fields provide access to icon
        /// handles, display names, type names, and attribute flags associated with files or folders. Callers are
        /// responsible for managing any native resources, such as icon handles, returned in this structure. Field
        /// values may be populated differently depending on the flags and parameters used with the corresponding Shell
        /// API call.</remarks>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            /// <summary>
            /// Specifies a handle to an icon associated with the object.
            /// </summary>
            /// <remarks>The value typically represents a native icon handle obtained from the
            /// operating system. The caller is responsible for managing the lifetime of the handle, including releasing
            /// any associated resources if required by the platform.</remarks>
            public nint hIcon;

            /// <summary>
            /// Specifies the zero-based index of the icon associated with the item.
            /// </summary>
            public int iIcon;

            /// <summary>
            /// Specifies the file or object attributes as a bitmask value.
            /// </summary>
            /// <remarks>The value typically represents a combination of attribute flags, such as
            /// read-only, hidden, or system, depending on the context in which it is used. Refer to the relevant API
            /// documentation for the meaning of each bit.</remarks>
            public uint dwAttributes;

            /// <summary>
            /// Specifies the display name associated with the item, typically used for user-friendly identification in
            /// the user interface.
            /// </summary>
            /// <remarks>The display name is limited to 260 characters, including the null terminator.
            /// This field is commonly used in interop scenarios where a fixed-length string buffer is required, such as
            /// when interacting with native Windows APIs.</remarks>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            /// <summary>
            /// Specifies the type name as a fixed-length string for interoperation with unmanaged code.
            /// </summary>
            /// <remarks>This field is intended for use in platform invocation scenarios where a
            /// fixed-size character buffer is required. The string is marshaled as a Unicode string with a maximum
            /// length of 80 characters, including the null terminator. If the assigned value exceeds this length, it
            /// will be truncated when marshaled to unmanaged code.</remarks>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint dwVolumeSerialNumber;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint nNumberOfLinks;
            public uint nFileIndexHigh;
            public uint nFileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8184)]
            public byte[] PathBuffer;
        }

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
        /// Retrieves information about a file or folder, such as its icon, display name, and type, from the Windows
        /// Shell.
        /// </summary>
        /// <remarks>This method is a P/Invoke signature for the native SHGetFileInfo function in
        /// shell32.dll. The caller is responsible for managing any resources, such as icon handles, returned by this
        /// method. For more information about valid flags and usage, see the Windows API documentation for
        /// SHGetFileInfo.</remarks>
        /// <param name="pszPath">The path to the file or folder for which information is to be retrieved. Can be null if certain flags are
        /// specified in <paramref name="uFlags"/>.</param>
        /// <param name="dwFileAttributes">The file attribute flags to use when determining the type of the file or folder. Used in combination with
        /// <paramref name="pszPath"/> to specify the attributes if the file does not exist.</param>
        /// <param name="psfi">When this method returns, contains a <see cref="SHFILEINFO"/> structure that receives the file information.</param>
        /// <param name="cbFileInfo">The size, in bytes, of the <see cref="SHFILEINFO"/> structure pointed to by <paramref name="psfi"/>.</param>
        /// <param name="uFlags">A combination of flags that specify what information to retrieve about the file or folder, such as icon,
        /// display name, or type name.</param>
        /// <returns>A handle to the icon specified in the <paramref name="uFlags"/> parameter if successful; otherwise, zero.</returns>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
#pragma warning disable SYSLIB1054
        private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
#pragma warning restore SYSLIB1054

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
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial nint CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

        /// <summary>
        /// Retrieves information about the specified file using its handle.
        /// </summary>
        /// <remarks>If the method returns false, call GetLastError to obtain extended error information.
        /// This method is a P/Invoke signature for the Windows API function GetFileInformationByHandle and is intended
        /// for advanced scenarios involving direct file handle manipulation.</remarks>
        /// <param name="hFile">A handle to the file for which information is to be retrieved. The handle must have been created with
        /// appropriate access rights.</param>
        /// <param name="lpFileInformation">When this method returns, contains a BY_HANDLE_FILE_INFORMATION structure that receives the file
        /// information.</param>
        /// <returns>true if the function succeeds; otherwise, false.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable SYSLIB1054
        private static extern bool GetFileInformationByHandle(nint hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
#pragma warning restore SYSLIB1054

        /// <summary>
        /// Closes an open object handle, releasing the associated system resources.
        /// </summary>
        /// <remarks>If the method returns false, call Marshal.GetLastWin32Error to retrieve the error
        /// code. Closing a handle that is already closed or invalid may result in an error. After a handle is closed,
        /// it must not be used in subsequent API calls.</remarks>
        /// <param name="hObject">A handle to an open object, such as a file, process, thread, or other system resource. The handle must have
        /// been obtained from a Windows API call that returns a handle.</param>
        /// <returns>true if the handle was closed successfully; otherwise, false.</returns>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(nint hObject);

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
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
#pragma warning disable SYSLIB1054
        private static extern bool DeviceIoControl(nint hDevice, uint dwIoControlCode, nint lpInBuffer, uint nInBufferSize, out REPARSE_DATA_BUFFER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, nint lpOverlapped);
#pragma warning restore SYSLIB1054

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
        private static partial bool GetSecurityInfo(nint handle, uint objectType, uint securityInfo, out nint ppsidOwner, out nint ppsidGroup, out nint ppDacl, out nint ppSacl, out nint ppSecurityDescriptor);

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
        [LibraryImport("kernel32.dll", EntryPoint = "LocalFreeW")]
        private static partial nint LocalFree(nint hMem);

        /// <summary>
        /// Retrieves comprehensive file information for a file or directory on Windows using native Win32 APIs.
        /// </summary>
        /// <remarks>This method provides detailed file metadata including attributes, timestamps, size,
        /// security information, and Shell-provided display data. Assumes the file exists at the specified path.
        /// Icon handles must be destroyed using DestroyIcon when no longer needed.</remarks>
        /// <param name="absolutePath">The absolute path to the file or directory. Must exist.</param>
        /// <returns>A <see cref="WindowsStorageInfo"/> structure containing comprehensive file information.</returns>
        /// <exception cref="Win32Exception">Thrown if the file information cannot be retrieved.</exception>
        public static WindowsStorageInfo GetWindowsFileInfo(string absolutePath)
        {
            var info = new WindowsStorageInfo();

            // Get Shell information (icon, display name, type name)
            SHGetFileInfo(absolutePath, 0, out SHFILEINFO shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), 
                (uint)(SHGFI.Icon | SHGFI.DisplayName | SHGFI.TypeName | SHGFI.Attributes));
            
            info.IconHandle = shinfo.hIcon;
            info.DisplayName = shinfo.szDisplayName;
            info.TypeName = shinfo.szTypeName;

            // Get detailed file information
            nint fileHandle = CreateFile(absolutePath, GENERIC_READ, FILE_SHARE_READ, nint.Zero, OPEN_EXISTING, 
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nint.Zero);

            if (fileHandle == new nint(-1))
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to open file handle for '{absolutePath}'.");
            }

            try
            {
                // Get file information by handle
                if (!GetFileInformationByHandle(fileHandle, out BY_HANDLE_FILE_INFORMATION fileInfo))
                {
                    int errorCode = Marshal.GetLastWin32Error();
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
            }
            finally
            {
                CloseHandle(fileHandle);
            }

            return info;
        }

        private static DateTime FileTimeToDateTime(FILETIME fileTime)
        {
            long fileTimeValue = ((long)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
            return DateTime.FromFileTimeUtc(fileTimeValue);
        }

        private static string? GetReparsePointTarget(nint fileHandle)
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
                return System.Text.Encoding.Unicode.GetString(reparseData.PathBuffer, offset * 2, length * 2);
            }

            return null;
        }

        #endregion

        #region Linux Implementation

        [StructLayout(LayoutKind.Sequential)]
        private struct LinuxStat
        {
            public ulong st_dev;
            public ulong st_ino;
            public uint st_mode;
            public ulong st_nlink;
            public uint st_uid;
            public uint st_gid;
            public ulong st_rdev;
            public long st_size;
            public long st_blksize;
            public long st_blocks;
            public long st_atim_sec;
            public long st_atim_nsec;
            public long st_mtim_sec;
            public long st_mtim_nsec;
            public long st_ctim_sec;
            public long st_ctim_nsec;
        }

#pragma warning disable SYSLIB1054
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int lstat(string pathname, ref LinuxStat stat_buf);

        [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int readlink(string pathname, byte[] buf, int bufsiz);
#pragma warning restore SYSLIB1054

        /// <summary>
        /// Retrieves file attributes and metadata for a specified file or symbolic link on a Linux file system.
        /// </summary>
        /// <remarks>This method uses the Linux lstat system call to obtain information about the
        /// specified file or symbolic link. Assumes the file exists at the specified path.</remarks>
        /// <param name="absolutePath">The absolute path to the file or symbolic link. Must exist.</param>
        /// <returns>A <see cref="UnixStorageInfo"/> object containing the file's attributes, ownership, size, timestamps, and
        /// symbolic link target information if applicable.</returns>
        /// <exception cref="Win32Exception">Thrown when the file attributes cannot be retrieved.</exception>
        public static UnixStorageInfo GetLinuxFileInfo(string absolutePath)
        {
            var info = new UnixStorageInfo();
            LinuxStat statBuf = new();

            if (lstat(absolutePath, ref statBuf) != 0)
            {
                int errno = Marshal.GetLastWin32Error();
                throw new Win32Exception(errno, $"Failed to get file info for '{absolutePath}'");
            }

            info.Mode = statBuf.st_mode;
            info.OwnerId = statBuf.st_uid;
            info.GroupId = statBuf.st_gid;
            info.Size = (ulong)statBuf.st_size;
            info.LastAccessTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_atim_sec).AddTicks(statBuf.st_atim_nsec / 100).DateTime;
            info.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_mtim_sec).AddTicks(statBuf.st_mtim_nsec / 100).DateTime;
            info.CreationTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_ctim_sec).AddTicks(statBuf.st_ctim_nsec / 100).DateTime;
            info.Attributes = MapUnixModeToAttributes(statBuf.st_mode);

            // Check for symbolic link and get target
            if ((statBuf.st_mode & 0xF000) == 0xA000)
            {
                // info.Attributes |= UnixFileAttributes.SymbolicLink | FileAttributes.ReparsePoint;
                
                byte[] buffer = new byte[4096];
                int len = readlink(absolutePath, buffer, buffer.Length - 1);
                if (len > 0)
                {
                    info.SymlinkTarget = System.Text.Encoding.UTF8.GetString(buffer, 0, len);
                }
            }

            return info;
        }

        #endregion

        #region macOS Implementation

        [StructLayout(LayoutKind.Sequential)]
        private struct MacStat
        {
            public int st_dev;           // ID of device containing file
            public ushort st_mode;       // File type and mode
            public ushort st_nlink;      // Number of hard links
            public ulong st_ino;         // File serial number
            public uint st_uid;         // User ID of the file
            public uint st_gid;         // Group ID of the file
            public int st_rdev;          // Device ID
            public Timespec st_atimespec; // Time of last access
            public Timespec st_mtimespec; // Time of last data modification
            public Timespec st_ctimespec; // Time of last status change
            public Timespec st_birthtimespec; // Time of file creation
            public long st_size;         // File size, in bytes
            public long st_blocks;       // Blocks allocated for file
            public int st_blksize;       // Optimal blocksize for I/O
            public uint st_flags;       // User defined flags for file
            public uint st_gen;         // File generation number
            public int st_lspare;
            public long st_qspare1;
            public long st_qspare2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Timespec
        {
            public long tv_sec;   // seconds
            public long tv_nsec;  // nanoseconds
        }

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        [DllImport("libc", SetLastError = true, EntryPoint = "stat$INODE64")]
        private static extern int mac_stat(string path, ref MacStat buf);

        [DllImport("libc", SetLastError = true, EntryPoint = "lstat$INODE64")]
        private static extern int mac_lstat(string path, ref MacStat buf);
#pragma warning restore SYSLIB1054

        /// <summary>
        /// Retrieves file attribute information for a file or directory on macOS systems.
        /// </summary>
        /// <remarks>This method uses native macOS system calls to obtain file metadata, including
        /// macOS-specific attributes. Assumes the file exists at the specified path.</remarks>
        /// <param name="absolutePath">The absolute path of the file or directory. Must exist.</param>
        /// <returns>A <see cref="UnixStorageInfo"/> object containing the file's attributes, ownership, size, and timestamps.</returns>
        /// <exception cref="Win32Exception">Thrown when the file information cannot be retrieved.</exception>
        public static UnixStorageInfo GetMacFileInfo(string absolutePath)
        {
            var info = new UnixStorageInfo();
            MacStat statBuf = new();

            if (mac_lstat(absolutePath, ref statBuf) != 0)
            {
                int errno = Marshal.GetLastWin32Error();
                throw new Win32Exception(errno, $"Failed to get file info for '{absolutePath}'");
            }

            info.Mode = statBuf.st_mode;
            info.OwnerId = statBuf.st_uid;
            info.GroupId = statBuf.st_gid;
            info.Size = (ulong)statBuf.st_size;
            info.LastAccessTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_atimespec.tv_sec).AddTicks(statBuf.st_atimespec.tv_nsec / 100).DateTime;
            info.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_mtimespec.tv_sec).AddTicks(statBuf.st_mtimespec.tv_nsec / 100).DateTime;
            info.CreationTime = DateTimeOffset.FromUnixTimeSeconds(statBuf.st_birthtimespec.tv_sec).AddTicks(statBuf.st_birthtimespec.tv_nsec / 100).DateTime;
            info.Attributes = MapUnixModeToAttributes(statBuf.st_mode);

            // macOS-specific hidden flag
            if ((statBuf.st_flags & 0x00000020) != 0)
            {
                info.Attributes |= FileAttributes.Hidden;
            }

            return info;
        }

        #endregion

        private static FileAttributes MapUnixModeToAttributes(uint mode)
        {
            FileAttributes attributes = 0;

            // File type
            switch (mode & 0xF000)
            {
                case 0x8000: // S_ISREG - regular file
                    attributes |= FileAttributes.Normal;
                    break;
                case 0x4000: // S_ISDIR - directory
                    attributes |= FileAttributes.Directory;
                    break;
                case 0xA000: // S_ISLNK - symbolic link
                    // Already handled above
                    break;
                case 0x2000: // S_ISCHR - character device
                    // attributes |= FileAttributes.CharacterDevice;
                    break;
                case 0x6000: // S_ISBLK - block device
                    // attributes |= FileAttributes.BlockDevice;
                    break;
                case 0x1000: // S_ISFIFO - FIFO/pipe
                    // attributes |= FileAttributes.NamedPipe;
                    break;
                case 0xC000: // S_ISSOCK - socket
                    // attributes |= FileAttributes.Socket;
                    break;
            }

            // Permissions
            if ((mode & 0x0100) == 0) // Owner read?
                attributes |= FileAttributes.ReadOnly;

            // Set UID bit
            // if ((mode & 0x0800) != 0)
                // attributes |= FileAttributes.SetUserId;

            // Set GID bit
            // if ((mode & 0x0400) != 0)
                // attributes |= FileAttributes.SetGroupId;

            // Sticky bit
            // if ((mode & 0x0200) != 0)
                // attributes |= FileAttributes.StickyBit;

            return attributes;
        }
    }
}
