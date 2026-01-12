using Rheo.Storage.Information;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rheo.Storage.COM
{
    [SupportedOSPlatform("macos")]
    internal static partial class MacOS
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Stat
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

        public static UnixStorageInfo GetFileInformation(string absolutePath)
        {
            var info = new UnixStorageInfo();

            if (lstat(absolutePath, out Stat statBuf) != 0)
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
            info.Attributes = Platform.MapUnixModeToAttributes(statBuf.st_mode);

            // macOS-specific hidden flag
            if ((statBuf.st_flags & 0x00000020) != 0)
            {
                info.Attributes |= FileAttributes.Hidden;
            }

            return info;
        }

        /// <summary>
        /// Invokes the native lstat function to retrieve file status information for the specified path, without
        /// following symbolic links.
        /// </summary>
        /// <remarks>Unlike stat, lstat does not follow symbolic links. If path refers to a symbolic link,
        /// information about the link itself is returned, not the target. This method is a platform invoke (P/Invoke)
        /// wrapper for the native lstat function and is intended for interop scenarios.</remarks>
        /// <param name="path">The path to the file or symbolic link for which to obtain status information. The path must be a valid,
        /// null-terminated UTF-8 string.</param>
        /// <param name="buf">When this method returns, contains a Stat structure with information about the file or symbolic link
        /// specified by path.</param>
        /// <returns>Zero if the operation succeeds; otherwise, a nonzero error code is returned and additional error information
        /// may be available via errno.</returns>
        // The library name is typically "System" on macOS and iOS, which maps to libSystem.dylib.
        [LibraryImport("System", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int lstat(string path, out Stat buf);
    }
}
