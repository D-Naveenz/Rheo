using System.Diagnostics.CodeAnalysis;

namespace Rheo.Storage
{
    /// <summary>
    /// Provides a synchronous implementation of the <see cref="IProgress{T}"/> interface that invokes a specified
    /// callback when progress is reported.
    /// </summary>
    /// <typeparam name="T">The type of progress update value.</typeparam>
    public class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncProgress{T}"/> class.
        /// </summary>
        /// <param name="callback">The callback to invoke when progress is reported.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
        [SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
        public SyncProgress(Action<T> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <inheritdoc/>
        public void Report(T value)
        {
            _callback(value);
        }
    }
}
