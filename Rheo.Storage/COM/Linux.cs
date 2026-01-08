using Rheo.Storage.Information;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rheo.Storage.COM
{
    [SupportedOSPlatform("linux")]
    internal static class Linux
    {
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

        /// <summary>
        /// Retrieves file attributes and metadata for a specified file or symbolic link on a Linux file system.
        /// </summary>
        /// <remarks>This method uses the Linux lstat system call to obtain information about the
        /// specified file or symbolic link. Assumes the file exists at the specified path.</remarks>
        /// <param name="absolutePath">The absolute path to the file or symbolic link. Must exist.</param>
        /// <returns>A <see cref="UnixStorageInfo"/> object containing the file's attributes, ownership, size, timestamps, and
        /// symbolic link target information if applicable.</returns>
        /// <exception cref="Win32Exception">Thrown when the file attributes cannot be retrieved.</exception>
        public static UnixStorageInfo GetFileInformation(string absolutePath)
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
            info.Attributes = Platform.MapUnixModeToAttributes(statBuf.st_mode);

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

#pragma warning disable SYSLIB1054
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int lstat(string pathname, ref LinuxStat stat_buf);

        [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int readlink(string pathname, byte[] buf, int bufsiz);
#pragma warning restore SYSLIB1054
    }
}
