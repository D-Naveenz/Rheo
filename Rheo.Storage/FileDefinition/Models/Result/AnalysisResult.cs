namespace Rheo.Storage.FileDefinition.Models.Result
{
    /// <summary>
    /// Represents the result of an analysis, including the set of definitions and their associated metadata such as
    /// file extensions and MIME types.
    /// </summary>
    /// <remarks>Use this class to access the analyzed definitions along with their unique file extensions and
    /// MIME types. The properties provide convenient access to distinct metadata derived from the underlying
    /// definitions. The collections exposed by this class are read-only after initialization.</remarks>
    public class AnalysisResult
    {
        /// <summary>
        /// Gets a value indicating whether the result contains no definitions.
        /// </summary>
        public bool IsEmpty => Definitions.Count == 0;

        /// <summary>
        /// Gets the collection of definitions along with their associated confidence scores.
        /// </summary>
        /// <remarks>Use this property to access all available definitions, each paired with a confidence
        /// value indicating the reliability or relevance of the definition. The collection is read-only after
        /// initialization.</remarks>
        public ConfidenceStack<Definition> Definitions { get; init; } = [];

        /// <summary>
        /// Gets a stack containing all unique file extensions defined in the current set of definitions.
        /// </summary>
        /// <remarks>The returned stack includes each extension only once, regardless of how many
        /// definitions reference it. The order of extensions in the stack reflects the order in which they are
        /// encountered when enumerating the definitions.</remarks>
        public ConfidenceStack<string> Extensions
        {
            get
            {
                var definitions = Definitions.Values;
                var stack = new ConfidenceStack<string>();
                foreach (var ext in definitions.SelectMany(d => d.Extensions))
                {
                    stack.Push(ext);
                }
                return stack;
            }
        }

        /// <summary>
        /// Gets a stack of unique MIME types defined in the current set of definitions.
        /// </summary>
        /// <remarks>The returned stack contains one entry for each distinct MIME type present in the
        /// definitions, with the most recently added types on top. The order of MIME types in the stack reflects the
        /// order in which they are encountered during enumeration.</remarks>
        public ConfidenceStack<string> MimeTypes
        {
            get
            {
                var definitions = Definitions.Values;
                var stack = new ConfidenceStack<string>();
                foreach (var mime in definitions.Select(d => d.MimeType))
                {
                    stack.Push(mime);
                }
                return stack;
            }
        }
    }
}
