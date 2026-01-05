using System.Runtime.InteropServices;

namespace Rheo.Storage.Information
{
    internal partial class InformationProvider
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
            public nint hIcon;                                            // Icon handle (must be destroyed by caller)
            public int iIcon;                                             // Icon index
            public uint dwAttributes;                                     // File attributes bitmask

            // Fixed-size buffers are Native AOT friendly
            public fixed char szDisplayName[260];                         // Display name (MAX_PATH = 260)
            public fixed char szTypeName[80];                             // Type description (max 80 chars)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        // File information structure from GetFileInformationByHandle
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

        // Reparse point data structure for symbolic links and junctions
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;
            public uint Flags;

            // Fixed-size buffers are Native AOT friendly
            public fixed byte PathBuffer[8184];                           // Buffer for link target path
        }

        #endregion

        #region Linux COM Types

        // Linux stat structure from lstat() syscall
        [StructLayout(LayoutKind.Sequential)]
        private struct LinuxStat
        {
            public ulong st_dev;        // Device ID
            public ulong st_ino;        // Inode number
            public uint st_mode;        // File type and permissions
            public ulong st_nlink;      // Number of hard links
            public uint st_uid;         // User ID
            public uint st_gid;         // Group ID
            public ulong st_rdev;       // Device ID (if special file)
            public long st_size;        // File size in bytes
            public long st_blksize;     // Block size for filesystem I/O
            public long st_blocks;      // Number of 512B blocks allocated
            public long st_atim_sec;    // Access time (seconds)
            public long st_atim_nsec;   // Access time (nanoseconds)
            public long st_mtim_sec;    // Modification time (seconds)
            public long st_mtim_nsec;   // Modification time (nanoseconds)
            public long st_ctim_sec;    // Status change time (seconds)
            public long st_ctim_nsec;   // Status change time (nanoseconds)
        }

        #endregion

        #region MacOS COM Types

        // macOS stat structure from lstat$INODE64() syscall
        [StructLayout(LayoutKind.Sequential)]
        private struct MacStat
        {
            public int st_dev;                      // Device ID
            public ushort st_mode;                  // File type and mode
            public ushort st_nlink;                 // Number of hard links
            public ulong st_ino;                    // Inode number
            public uint st_uid;                     // User ID
            public uint st_gid;                     // Group ID
            public int st_rdev;                     // Device ID (if special file)
            public Timespec st_atimespec;           // Access time
            public Timespec st_mtimespec;           // Modification time
            public Timespec st_ctimespec;           // Status change time
            public Timespec st_birthtimespec;       // Creation time (macOS-specific)
            public long st_size;                    // File size in bytes
            public long st_blocks;                  // Number of blocks allocated
            public int st_blksize;                  // Optimal block size for I/O
            public uint st_flags;                   // User-defined flags (macOS-specific)
            public uint st_gen;                     // File generation number
            public int st_lspare;
            public long st_qspare1;
            public long st_qspare2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Timespec
        {
            public long tv_sec;     // Seconds since epoch
            public long tv_nsec;    // Nanoseconds
        }

        #endregion
    }
}
