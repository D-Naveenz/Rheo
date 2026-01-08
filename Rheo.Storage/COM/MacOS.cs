using Rheo.Storage.Information;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rheo.Storage.COM
{
    [SupportedOSPlatform("macos")]
    internal static class MacOS
    {
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

        /// <summary>
        /// Retrieves file attribute information for a file or directory on macOS systems.
        /// </summary>
        /// <remarks>This method uses native macOS system calls to obtain file metadata, including
        /// macOS-specific attributes. Assumes the file exists at the specified path.</remarks>
        /// <param name="absolutePath">The absolute path of the file or directory. Must exist.</param>
        /// <returns>A <see cref="UnixStorageInfo"/> object containing the file's attributes, ownership, size, and timestamps.</returns>
        /// <exception cref="Win32Exception">Thrown when the file information cannot be retrieved.</exception>
        public static UnixStorageInfo GetFileInformation(string absolutePath)
        {
            var info = new UnixStorageInfo();
            MacStat statBuf = new();

            if (lstat(absolutePath, ref statBuf) != 0)
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

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        [DllImport("libc", SetLastError = true, EntryPoint = "stat$INODE64", CharSet = CharSet.Unicode)]
        private static extern int stat(string path, ref MacStat buf);

        [DllImport("libc", SetLastError = true, EntryPoint = "lstat$INODE64", CharSet = CharSet.Unicode)]
        private static extern int lstat(string path, ref MacStat buf);
#pragma warning restore SYSLIB1054
    }
}
