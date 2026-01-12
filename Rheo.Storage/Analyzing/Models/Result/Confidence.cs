namespace Rheo.Storage.Analyzing.Models.Result
{
    
    /// <summary>
    /// Represents a subject and its associated confidence score as a percentage.
    /// </summary>
    /// <remarks>Use this struct to associate a confidence value with a specific subject, such as a
    /// prediction, classification, or result. The confidence score indicates the degree of certainty or reliability for
    /// the given subject. This type is immutable and thread-safe.</remarks>
    /// <typeparam name="T">The type of the subject to which the confidence score applies. Must be non-null.</typeparam>
    /// <param name="subject">The subject for which the confidence score is calculated. Cannot be null.</param>
    /// <param name="value">The confidence score as a percentage, typically between 0.0 and 100.0.</param>
    public readonly struct Confidence<T>(T subject, double value) : IEquatable<Confidence<T>> where T : notnull
    {
        /// <summary>
        /// Gets the subject associated with this instance.
        /// </summary>
        public T Subject { get; init; } = subject;

        /// <summary>
        /// Gets the confidence score as a percentage based on the current score and the total possible score.
        /// </summary>
        /// <remarks>The confidence score is calculated as the ratio of the current score to the total
        /// score, expressed as a percentage. If the total score is zero, the confidence is zero.</remarks>
        public double Value { get; init; } = value;

        /// <inheritdoc/>
        public bool Equals(Confidence<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Subject, other.Subject);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Confidence<T> confidence && Equals(confidence);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Subject} ({Value:F2}%)";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Subject);
        }

        /// <inheritdoc/>
        public static bool operator ==(Confidence<T> left, Confidence<T> right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(Confidence<T> left, Confidence<T> right)
        {
            return !(left == right);
        }
    }
}
