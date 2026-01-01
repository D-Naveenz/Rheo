using System.Collections;

namespace Rheo.Storage.FileDefinition.Models.Result
{
    /// <summary>
    /// Represents a collection that tracks unique values and their associated confidence scores, allowing items to be
    /// added, updated, removed, and enumerated in order of descending score.
    /// </summary>
    /// <remarks>Each value in the collection is associated with an integer confidence score. Adding a value
    /// that already exists increases its score by the specified amount. Items can be removed individually or all at
    /// once, and enumeration yields results ordered by descending score. This collection is not thread-safe.</remarks>
    /// <typeparam name="T">The type of values stored in the collection. Must be non-nullable.</typeparam>
    public class ConfidenceStack<T> : IEnumerable<Confidence<T>>, ICollection<Confidence<T>> where T : notnull
    {
        private readonly Dictionary<T, int> _dict = [];

        /// <summary>
        /// Gets the element at the specified index in the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the element to retrieve.</param>
        /// <returns>The element at the specified index.</returns>
        public Confidence<T> this[int index]
        {
            get
            {
                return ToList()[index];
            }
        }

        /// <summary>
        /// Gets the sum of the Score values for all items in the collection.
        /// </summary>
        public int TotalScore => _dict.Values.Sum();

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count => _dict.Count;

        /// <summary>
        /// Gets an enumerable collection containing all values in the collection.
        /// </summary>
        public IEnumerable<T> Values => _dict.Keys;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Returns an enumerator that iterates through the collection of confidence results.
        /// </summary>
        /// <returns>An enumerator for the collection of <see cref="Confidence{T}"/> objects.</returns>
        public IEnumerator<Confidence<T>> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Adds the specified value to the collection with the given score, or increases its score if it already
        /// exists.
        /// </summary>
        /// <param name="value">The value to add to the collection or whose score to increase.</param>
        /// <param name="score">The score to associate with the value. Defaults to 1. Must be a positive integer.</param>
        public void Push(T value, int score = 1)
        {
            if (_dict.TryAdd(value, score))
            {
                _dict[value] += score;
            }
        }

        /// <summary>
        /// Removes and returns the item with the highest confidence score from the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="Confidence{T}"/> representing the item with the highest confidence score,
        /// or <c>null</c> if the collection is empty.
        /// </returns>
        public Confidence<T>? Pop()
        {
            var top = ToList().FirstOrDefault();
            if (top != default)
            {
                _dict.Remove(top.Subject);
            }
            return top;
        }

        /// <summary>
        /// Removes the specified value from the collection and returns its associated confidence result.
        /// </summary>
        /// <param name="subject">The value to remove from the collection. Must exist in the collection.</param>
        /// <returns>A ConfidenceResult<T> containing the removed value and its associated confidence score.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the specified value does not exist in the collection.</exception>
        public Confidence<T> Pop(T subject)
        {
            if (_dict.Remove(subject, out int value))
            {
                return new Confidence<T>(subject, GetConfidencePercentage(value));
            }
            throw new KeyNotFoundException("The specified value was not found in the collection.");
        }

        /// <inheritdoc/>
        public void Add(Confidence<T> item)
        {
            Push(item.Subject, GetConfidenceScore(item.Value));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _dict.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(Confidence<T> item)
        {
            return _dict.ContainsKey(item.Subject);
        }

        /// <inheritdoc/>
        public void CopyTo(Confidence<T>[] array, int arrayIndex)
        {
            foreach (var kvp in _dict)
            {
                array[arrayIndex++] = new Confidence<T>(kvp.Key, GetConfidencePercentage(kvp.Value));
            }
        }

        /// <inheritdoc/>
        public bool Remove(Confidence<T> item)
        {
            return _dict.Remove(item.Subject);
        }

        /// <summary>
        /// Creates a list of confidence values for each item, ordered from highest to lowest confidence.
        /// </summary>
        /// <remarks>The returned list is a snapshot of the collection at the time of access. Subsequent
        /// changes to the underlying collection are not reflected in the returned list.</remarks>
        /// <returns>A list of <see cref="Confidence{T}"/> objects representing each item's confidence percentage, sorted in
        /// descending order by confidence value.</returns>
        public List<Confidence<T>> ToList()
        {
            return [.. _dict
                .Select(kvp => new Confidence<T>(kvp.Key, GetConfidencePercentage(kvp.Value)))
                .OrderByDescending(cr => cr.Value)];
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ConfidenceStack(Count={Count}, TotalScore={TotalScore})";
        }

        private double GetConfidencePercentage(int value)
        {
            if (TotalScore == 0)
            {
                return 0.0;
            }
            return (double)value / TotalScore * 100.0;
        }

        private int GetConfidenceScore(double percentage)
        {
            if (percentage <= 0.0)
            {
                return 0;
            }
            return (int)Math.Round((percentage / 100.0) * TotalScore);
        }  
    }
}
