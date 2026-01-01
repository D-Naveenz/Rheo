namespace Rheo.Storage.FileDefinition.Models
{
    public class AnalysisResult
    {
        /// <summary>
        /// Gets the definition associated with this instance.
        /// </summary>
        public required Definition Definition { get; init; }

        /// <summary>
        /// Gets the number of points associated with the instance.
        /// </summary>
        public int Points { get; init; }

        /// <summary>
        /// Gets or sets the confidence score representing the likelihood that the result is correct.
        /// </summary>
        /// <remarks>The confidence score is typically a percentage value between 0.0 and 100.0, where higher values
        /// indicate greater certainty. The interpretation of this value may vary depending on the context in which it
        /// is used.</remarks>
        public double Confidence { get; set; }
    }
}
