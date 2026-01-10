using Rheo.Storage.Information;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rheo.Storage.COM
{
    [SupportedOSPlatform("linux")]
    internal static partial class Linux
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Stat
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

        public static UnixStorageInfo GetFileInformation(string absolutePath)
        {
            var info = new UnixStorageInfo();
            Stat statBuf = new();

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

        /// <summary>
        /// Retrieves information about the file or symbolic link specified by the given path, without following
        /// symbolic links.
        /// </summary>
        /// <remarks>This method corresponds to the native lstat function in libc. Unlike stat, lstat does
        /// not follow symbolic links, and instead returns information about the link itself. This method is intended
        /// for interop scenarios and should be used with care, as improper usage may lead to resource leaks or
        /// undefined behavior.</remarks>
        /// <param name="pathname">The path to the file or symbolic link for which to retrieve status information. The path must be valid and
        /// encoded as UTF-8.</param>
        /// <param name="stat_buf">When this method returns, contains a structure populated with information about the specified file or
        /// symbolic link.</param>
        /// <returns>Zero if the operation succeeds; otherwise, -1. If the operation fails, additional error information can be
        /// obtained using the system error code.</returns>
        [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int lstat(string pathname, ref Stat stat_buf);

        /// <summary>
        /// Reads the value of a symbolic link and stores it in the specified buffer.
        /// </summary>
        /// <remarks>The buffer is not null-terminated. To obtain the full contents of the symbolic link,
        /// use the number of bytes returned. If the buffer is too small, the result is truncated. This method sets the
        /// system error code on failure; use Marshal.GetLastWin32Error to retrieve the error code.</remarks>
        /// <param name="pathname">The path to the symbolic link to be read. Cannot be null.</param>
        /// <param name="buf">The buffer that receives the contents of the symbolic link. Must be large enough to hold the link's
        /// contents.</param>
        /// <param name="bufsiz">The size of the buffer, in bytes. Must be greater than zero.</param>
        /// <returns>The number of bytes placed in the buffer on success; otherwise, -1 if an error occurs.</returns>
        [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int readlink(string pathname, byte[] buf, int bufsiz);
    }
}
