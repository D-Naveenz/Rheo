using Rheo.Storage.Analyzing;
using Rheo.Storage.Analyzing.Models.Result;
using System.ComponentModel;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides detailed information and analysis results for a specific file, including type, MIME type, extension,
    /// and content-based identification. This class enables inspection of file properties and content-derived metadata
    /// in a read-only, immutable manner.
    /// </summary>
    /// <remarks>FileInformation defers file content analysis until identification results are first accessed,
    /// which helps avoid file locks during object construction. Instances are immutable and thread-safe. Use this class
    /// to obtain both file system and content-based metadata for a file, such as its detected type, MIME type, and
    /// actual extension. Inherits from StorageInformation and implements value equality based on file path, size, and
    /// identification results.</remarks>
    [ImmutableObject(true)]
    public sealed class FileInformation : StorageInformation, IEquatable<FileInformation>
    {
        private readonly Lazy<AnalysisResult> _identificationReportLazy;

        /// <summary>
        /// Initializes a new instance of the FileInformation class for the specified file path.
        /// </summary>
        /// <remarks>The file analysis is deferred until analysis results are first accessed, 
        /// preventing file locks during object construction.</remarks>
        /// <param name="absolutePath">The absolute path to the file to be analyzed. The file must exist at the specified location.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file specified by absolutePath does not exist.</exception>
        public FileInformation(string absolutePath) : base(absolutePath)
        {
            // Validate Path
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("The specified file does not exist.", absolutePath);
            }

            // Defer analysis until first access to avoid file locks during construction
            _identificationReportLazy = new Lazy<AnalysisResult>(() => FileAnalyzer.AnalyzeFile(absolutePath));
        }

        #region Properties: Core Identity
        /// <summary>
        /// The user-friendly description of the type. This is typically a description of the storage type (e.g., "Text Document").
        /// </summary>
        public string TypeName
        {
            get
            {
                string? typeNameByInfo = null;
                if (TryGetWindowsStorageInfo(out var winfo))
                {
                    return winfo.DisplayName;
                }

                var definition = _identificationReportLazy.Value.Definitions.FirstOrDefault().Subject;
                string? typeNameByDefinition = definition?.FileType;

                // Prefer the longer, more descriptive type name
                if (!string.IsNullOrEmpty(typeNameByInfo) && !string.IsNullOrEmpty(typeNameByDefinition))
                {
                    return typeNameByInfo.Length >= typeNameByDefinition.Length ? typeNameByInfo : typeNameByDefinition;
                }
                else if (!string.IsNullOrEmpty(typeNameByInfo))
                {
                    return typeNameByInfo;
                }
                else if (!string.IsNullOrEmpty(typeNameByDefinition))
                {
                    return typeNameByDefinition;
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        /// <summary>
        /// The MIME (Multipurpose Internet Mail Extensions) type of the storage. For example, a music file might have the "audio/mpeg" MIME type.
        /// </summary>
        public Confidence<string> MimeType => _identificationReportLazy.Value.MimeTypes.FirstOrDefault();

        /// <summary>
        /// The file extension (including the dot), if available, as determined by the file name or path.
        /// </summary>
        public string? Extension => Path.GetExtension(AbsolutePath);

        /// <summary>
        /// The actual extension determined by file content analysis.
        /// </summary>
        public Confidence<string> ActualExtension => _identificationReportLazy.Value.Extensions.FirstOrDefault();

        /// <summary>
        /// The result of file type analysis, including detected definitions, extensions, and MIME types.
        /// </summary>
        public AnalysisResult IdentificationReport => _identificationReportLazy.Value;

        #endregion

        #region Properties: Size
        /// <inheritdoc/>
        public override long Size => (long)_storageInfoLazy.Value.Size;

        /// <inheritdoc/>
        public bool Equals(FileInformation? other)
        {
            if (other is null)
            {
                return false;
            }
            return string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.OrdinalIgnoreCase) &&
                   Size == other.Size &&
                   ActualExtension == other.ActualExtension &&
                   MimeType.Equals(other.MimeType);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as FileInformation);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(AbsolutePath.ToLowerInvariant(), Size, ActualExtension, MimeType);
        }

        /// <inheritdoc/>
        public static bool operator ==(FileInformation? left, FileInformation? right)
        {
            if (left is null)
            {
                return right is null;
            }
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(FileInformation? left, FileInformation? right)
        {
            return !(left == right);
        }

        #endregion

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"[{ActualExtension}] {DisplayName} ({FormattedSize})";
        }
    }
}
