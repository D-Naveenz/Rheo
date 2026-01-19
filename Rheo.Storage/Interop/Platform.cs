using Rheo.Storage.Contracts;
using System.Runtime.Versioning;

namespace Rheo.Storage.Interop
{
    internal static class Platform
    {
        public static IStorageInfoStruct GetStorageInformation(string absolutePath)
        {
            try
            {
                IStorageInfoStruct infoStruct;

                if (OperatingSystem.IsWindows())
                {
                    infoStruct = Win32.GetFileInformation(absolutePath);
                }
                else if (OperatingSystem.IsLinux())
                {
                    infoStruct = Linux.GetFileInformation(absolutePath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    infoStruct = MacOS.GetFileInformation(absolutePath);
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

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static FileAttributes MapUnixModeToAttributes(uint mode)
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
