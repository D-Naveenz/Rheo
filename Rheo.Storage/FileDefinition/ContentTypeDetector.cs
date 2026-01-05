using Rheo.Storage.FileDefinition.Models;
using System.Text;

namespace Rheo.Storage.FileDefinition
{
    /// <summary>
    /// Provides methods for detecting content types and distinguishing between text and binary files.
    /// </summary>
    /// <remarks>This class analyzes file content to determine whether a file is plain text, uses a specific
    /// encoding (UTF-8, UTF-16, UTF-32), or is binary data. It supports Byte Order Mark (BOM) detection and
    /// heuristic-based text detection for files without patterns.</remarks>
    internal static class ContentTypeDetector
    {
        private const int NULL_BYTE_THRESHOLD_PERCENTAGE = 1; // 1% null bytes indicates binary

        /// <summary>
        /// Detects the encoding of the specified buffer by checking for Byte Order Marks (BOMs).
        /// </summary>
        /// <param name="buffer">The byte array to analyze for BOM presence. Must not be null.</param>
        /// <param name="encoding">When this method returns, contains the detected encoding if a BOM is found; otherwise, null.</param>
        /// <returns><see langword="true"/> if a BOM was detected and the encoding was identified; otherwise, <see langword="false"/>.</returns>
        internal static bool TryDetectBOM(byte[] buffer, out Encoding? encoding)
        {
            encoding = null;

            if (buffer.Length < 2)
                return false;

            // UTF-32 BE BOM
            if (buffer.Length >= 4 && buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
            {
                encoding = new UTF32Encoding(true, true);
                return true;
            }

            // UTF-32 LE BOM
            if (buffer.Length >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
            {
                encoding = new UTF32Encoding(false, true);
                return true;
            }

            // UTF-8 BOM
            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                encoding = Encoding.UTF8;
                return true;
            }

            // UTF-16 BE BOM
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
                return true;
            }

            // UTF-16 LE BOM
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                encoding = Encoding.Unicode;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified buffer contains text content.
        /// </summary>
        /// <remarks>This method uses multiple heuristics to detect text content: BOM detection for encoded text,
        /// null byte analysis for binary detection, and character analysis for plain ASCII/UTF-8 text. The method is
        /// designed to be robust and handle various text encodings including ASCII, UTF-8, UTF-16, and UTF-32.</remarks>
        /// <param name="buffer">The byte array to analyze. Must not be null or empty.</param>
        /// <returns><see langword="true"/> if the buffer is likely to contain text; <see langword="false"/> if it appears to be binary data.</returns>
        internal static bool IsTextContent(byte[] buffer)
        {
            if (buffer.Length == 0)
                return false;

            // Check for BOM - if present, it's definitely a text file
            if (TryDetectBOM(buffer, out _))
                return true;

            // Count null bytes and control characters
            int nullBytes = 0;
            int controlChars = 0;
            int printableChars = 0;
            int extendedAscii = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                byte b = buffer[i];

                if (b == 0x00)
                {
                    nullBytes++;
                }
                else if (b < 32)
                {
                    // Allow common whitespace characters (tab, newline, carriage return)
                    if (b == 0x09 || b == 0x0A || b == 0x0D)
                    {
                        printableChars++;
                    }
                    else
                    {
                        controlChars++;
                    }
                }
                else if (b < 127)
                {
                    // Printable ASCII
                    printableChars++;
                }
                else if (b >= 128)
                {
                    // Extended ASCII or UTF-8 continuation bytes
                    extendedAscii++;
                }
            }

            // If more than threshold percentage are null bytes, it's binary
            double nullPercentage = nullBytes * 100.0 / buffer.Length;
            if (nullPercentage > NULL_BYTE_THRESHOLD_PERCENTAGE)
                return false;

            // If there are too many control characters relative to printable characters, it's likely binary
            if (controlChars > printableChars / 2)
                return false;

            // Check for valid UTF-8 sequences if extended ASCII is present
            if (extendedAscii > 0)
            {
                if (IsValidUTF8(buffer))
                    return true;
            }

            // If mostly printable characters, consider it text
            double printablePercentage = (printableChars + extendedAscii) * 100.0 / buffer.Length;
            return printablePercentage > 75;
        }

        /// <summary>
        /// Creates a fallback definition for files that could not be identified by pattern matching.
        /// </summary>
        /// <remarks>This method determines whether the content is text or binary and returns an appropriate
        /// definition with low confidence. It's intended as a last resort for files without recognizable patterns.</remarks>
        /// <param name="buffer">The file content buffer to analyze. Must not be null.</param>
        /// <param name="filePath">The path to the file being analyzed, used for extension-based fallback.</param>
        /// <returns>A <see cref="Definition"/> object representing either a plain text file or a generic binary file.</returns>
        internal static Definition CreateFallbackDefinition(byte[] buffer, string filePath)
        {
            string extension = Path.GetExtension(filePath).TrimStart('.');
            bool isText = IsTextContent(buffer);

            return isText
                ? new Definition
                {
                    FileType = "Plain Text",
                    Extensions = string.IsNullOrEmpty(extension) ? ["txt"] : [extension],
                    MimeType = "text/plain",
                    Remarks = "Detected as text content (no pattern match found)",
                    Signature = new Signature(),
                    PriorityLevel = -1000 // Very low priority
                }
                : new Definition
                {
                    FileType = "Binary Data",
                    Extensions = string.IsNullOrEmpty(extension) ? ["bin"] : [extension],
                    MimeType = "application/octet-stream",
                    Remarks = "Detected as binary content (no pattern match found)",
                    Signature = new Signature(),
                    PriorityLevel = -1000 // Very low priority
                };
        }

        /// <summary>
        /// Validates whether the specified buffer contains valid UTF-8 encoded text.
        /// </summary>
        /// <remarks>This method checks if the byte sequence conforms to UTF-8 encoding rules, including proper
        /// multi-byte character sequences. It does not check for BOMs.</remarks>
        /// <param name="buffer">The byte array to validate for UTF-8 encoding. Must not be null.</param>
        /// <returns><see langword="true"/> if the buffer contains valid UTF-8 sequences; otherwise, <see langword="false"/>.</returns>
        private static bool IsValidUTF8(byte[] buffer)
        {
            int i = 0;
            while (i < buffer.Length)
            {
                byte b = buffer[i];

                if (b < 0x80)
                {
                    // Single-byte character (ASCII)
                    i++;
                }
                else if ((b & 0xE0) == 0xC0)
                {
                    // 2-byte character
                    if (i + 1 >= buffer.Length || (buffer[i + 1] & 0xC0) != 0x80)
                        return false;
                    i += 2;
                }
                else if ((b & 0xF0) == 0xE0)
                {
                    // 3-byte character
                    if (i + 2 >= buffer.Length || (buffer[i + 1] & 0xC0) != 0x80 || (buffer[i + 2] & 0xC0) != 0x80)
                        return false;
                    i += 3;
                }
                else if ((b & 0xF8) == 0xF0)
                {
                    // 4-byte character
                    if (i + 3 >= buffer.Length || (buffer[i + 1] & 0xC0) != 0x80 || (buffer[i + 2] & 0xC0) != 0x80 || (buffer[i + 3] & 0xC0) != 0x80)
                        return false;
                    i += 4;
                }
                else
                {
                    // Invalid UTF-8 start byte
                    return false;
                }
            }

            return true;
        }
    }
}