using Rheo.Storage.Analysing;
using Rheo.Storage.Analysing.Models.Result;
using Rheo.Storage.Contracts;

namespace Rheo.Storage.Information
{
    /// <summary>
    /// Provides detailed information about a file, including its type, MIME type, extension, and analysis results,
    /// based on both file metadata and content analysis.
    /// </summary>
    /// <remarks>Use this class to access file-specific information and analysis results, such as the detected
    /// file type, MIME type, and extensions. FileInformation performs content-based analysis in addition to using file
    /// metadata, which can provide more accurate identification than relying on file name or extension alone. This
    /// class is typically used when you need to determine the true nature of a file, regardless of its name or
    /// extension.</remarks>
    public sealed class FileInformation : StorageInformation, IEquatable<FileInformation>
    {
        private readonly TaskCompletionSource<AnalysisResult> _analysisTaskAwaiter;
        private readonly Lazy<AnalysisResult> _identificationReportLazy;

        /// <summary>
        /// Initializes a new instance of the FileInformation class for the specified file path and begins asynchronous
        /// analysis of the file.
        /// </summary>
        /// <remarks>The file analysis is started in the background upon construction. Accessing analysis
        /// results may block until the analysis is complete.</remarks>
        /// <param name="absolutePath">The absolute path to the file to be analyzed. The file must exist at the specified location.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file specified by absolutePath does not exist.</exception>
        public FileInformation(string absolutePath) : base(absolutePath)
        {
            // Validate Path
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("The specified file does not exist.", absolutePath);
            }

            // Initialize the task completion source
            _analysisTaskAwaiter = new TaskCompletionSource<AnalysisResult>();
            
            // Start the analysis in a background task
            Task.Run(() =>
            {
                try
                {
                    var report = FileAnalyzer.AnalyzeFile(absolutePath);
                    _analysisTaskAwaiter.SetResult(report);
                }
                catch (Exception ex)
                {
                    _analysisTaskAwaiter.SetException(ex);
                }
            });
            
            _identificationReportLazy = new Lazy<AnalysisResult>(() => _analysisTaskAwaiter.Task.GetAwaiter().GetResult());
        }

        /// <summary>
        /// Creates a new instance of the specified storage information type for the given absolute path.
        /// </summary>
        /// <typeparam name="TInfo">The type of storage information to create. Must implement <see cref="IStorageInformation"/>.</typeparam>
        /// <param name="absolutePath">The absolute path to the storage resource for which information is to be created. Cannot be null or empty.</param>
        /// <returns>An instance of <typeparamref name="TInfo"/> representing the storage information for the specified path.</returns>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TInfo"/> is not supported for creation.</exception>
        public static new TInfo Create<TInfo>(string absolutePath) where TInfo : IStorageInformation
        {
            if (typeof(TInfo) == typeof(FileInformation))
            {
                return (TInfo)(IStorageInformation)new FileInformation(absolutePath);
            }
            else
            {
                throw new NotSupportedException($"The type '{typeof(TInfo).FullName}' is not supported for creation.");
            }
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
        public string? Extension => Path.GetExtension(_absPath);

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
            return string.Equals(_absPath, other._absPath, StringComparison.OrdinalIgnoreCase) &&
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
            return HashCode.Combine(_absPath.ToLowerInvariant(), Size, ActualExtension, MimeType);
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
