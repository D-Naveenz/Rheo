using Microsoft.Extensions.Configuration;
using Rheo.Extensions.Hosting.Commandline;
using System.Diagnostics.CodeAnalysis;

namespace Rheo.Extensions.Hosting.Argument
{
    /// <summary>
    /// Represents a collection of argument handlers that supports efficient lookup, task management, and  operations
    /// for adding, removing, and retrieving handlers and their associated tasks.
    /// </summary>
    /// <remarks>This collection is designed to manage instances of <see cref="IArgumentHandler"/> and their
    /// associated  tasks. It provides methods for adding handlers, retrieving tasks by key, and removing handlers while
    /// maintaining internal mappings for efficient lookup. The collection also supports operations such as  clearing
    /// all handlers and retrieving all tasks.  The collection automatically updates its internal mappings when handlers
    /// are added or removed. Handlers  must define valid keys in their <see cref="IArgumentHandler.Keys"/> property to
    /// be added to the collection.</remarks>
    /// <param name="configuration"></param>
    public class ArgumentHandlerCollection(IConfiguration configuration, IConsoleService consoleService) : List<IArgumentHandler>
    {
        public const string ArgHandlersListKey = "CommandlineArgHandlers";

        private readonly Dictionary<string, ArgumentKeys> _argumantKeyMap = [];
        private readonly Dictionary<ArgumentKeys, Func<CancellationToken, Task>> _taskCollection = [];

        /// <summary>
        /// Gets the task associated with the specified key.
        /// </summary>
        /// <remarks>This indexer retrieves a task based on the provided key. If the key does not exist in
        /// the collection, a <see cref="KeyNotFoundException"/> is thrown. Ensure the key is valid and corresponds to
        /// an existing task.</remarks>
        /// <param name="key">The key used to retrieve the associated task. Cannot be <see langword="null"/> or empty.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">Thrown if no task is found for the specified key.</exception>
        public Task this[string key, CancellationToken cancellationToken = default] => 
            RunTask(key, cancellationToken) ?? throw new KeyNotFoundException($"No task found for key '{key}'.");

        /// <summary>
        /// Adds the specified argument handler to the collection and updates the internal mappings for easy lookup.
        /// </summary>
        /// <remarks>This method updates the internal key mappings to allow efficient lookup of handlers
        /// by their keys.  If the handler defines both a short key and a long key, both keys are mapped for lookup
        /// purposes.</remarks>
        /// <param name="handler">The argument handler to add. The handler must have valid keys defined in its <see
        /// cref="IArgumentHandler.Keys"/> property.</param>
        [RequiresUnreferencedCode("Calls IconTeX.Host.Activation.IActivationHandler.CanHandle(IConfiguration)")]
        public new void Add(IArgumentHandler handler)
        {
            // Add the reference to the collection using its key
            base.Add(handler);

            // Map the key to the ArgumentKeys for easy lookup
            _argumantKeyMap[handler.Keys.Key] = handler.Keys;
            if (!string.IsNullOrEmpty(handler.Keys.LongKey) && handler.Keys.LongKey != handler.Keys.Key)
            {
                _argumantKeyMap[handler.Keys.LongKey!] = handler.Keys;
            }

            // Build the task for the handler and store it in the TaskCollection
            _taskCollection[handler.Keys] = (token) => handler.HandleAsync(configuration, consoleService, token);
        }

        /// <summary>
        /// Adds a collection of <see cref="IArgumentHandler"/> instances to the current set of handlers.
        /// </summary>
        /// <remarks>Each handler in the provided collection is added individually using the <see
        /// cref="Add"/> method.</remarks>
        /// <param name="handlers">The collection of <see cref="IArgumentHandler"/> instances to add. Cannot be null.</param>
        [RequiresUnreferencedCode("Calls IconTeX.Host.Activation.IActivationHandler.CanHandle(IConfiguration)")]
        public new void AddRange(IEnumerable<IArgumentHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                Add(handler);
            }
        }

        /// <summary>
        /// Removes all elements from the collection and clears associated mappings and tasks.
        /// </summary>
        /// <remarks>This method clears the base collection as well as any additional internal mappings
        /// and task collections. After calling this method, the collection and its associated data will be
        /// empty.</remarks>
        public new void Clear()
        {
            base.Clear();
            _argumantKeyMap.Clear();
            _taskCollection.Clear();
        }

        /// <summary>
        /// Executes a task associated with the specified key.
        /// </summary>
        /// <param name="key">The key used to identify the task to execute. Must correspond to a valid task in the internal mapping.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no task is found for the specified <paramref name="key"/>.</exception>
        public Task RunTask(string key, CancellationToken cancellationToken = default)
        {
            if (_argumantKeyMap.TryGetValue(key, out var keys) && keys != null)
            {
                return RunTask(keys, cancellationToken);
            }
            throw new KeyNotFoundException($"No task found for key '{key}'.");
        }

        /// <summary>
        /// Executes the task associated with the specified key.
        /// </summary>
        /// <param name="key">The key used to identify the task to execute.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the task execution. The default value is <see
        /// cref="CancellationToken.None"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of the executed task.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no task is found for the specified <paramref name="key"/>.</exception>
        public Task RunTask(ArgumentKeys key, CancellationToken cancellationToken = default)
        {
            if (_taskCollection.TryGetValue(key, out var task))
            {
                return task(cancellationToken);
            }

            throw new KeyNotFoundException($"No task found for key '{key.Key}'.");
        }

        /// <summary>
        /// Executes all tasks in the collection and returns the resulting tasks.
        /// </summary>
        /// <remarks>The returned tasks will complete independently, and their completion status (success,
        /// failure, or cancellation) can be observed individually. If the <paramref name="cancellationToken"/> is
        /// triggered, it will propagate to the tasks, but the behavior of each task depends on its
        /// implementation.</remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the execution of the tasks.</param>
        /// <returns>An array of <see cref="Task"/> objects representing the asynchronous operations initiated by the method.
        /// Each task corresponds to an individual operation in the collection.</returns>
        public Task[] RunAllTasks(CancellationToken cancellationToken = default) => 
            [.. _taskCollection.Values.Select(t => t(cancellationToken))];

        /// <summary>
        /// Removes the specified argument handler and its associated data from the collection.
        /// </summary>
        /// <remarks>This method removes the handler from the base collection, as well as any associated
        /// data  from internal mappings. The removal is considered successful only if all related data  is
        /// removed.</remarks>
        /// <param name="handler">The argument handler to remove. Must not be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the handler and its associated data were successfully removed;  otherwise, <see
        /// langword="false"/>.</returns>
        public new bool Remove(IArgumentHandler handler)
        {
            var key = handler.Keys.Key;
            var longKey = handler.Keys.LongKey;

            // Get results from removing data associated with both keys
            var taskRemoved = _taskCollection.Remove(handler.Keys);
            var mapRemoved = _argumantKeyMap.Remove(key) || longKey != null && longKey != key && _argumantKeyMap.Remove(longKey);
            var handlerRemoved = base.Remove(handler);

            // Return true if any removal was successful
            return taskRemoved && mapRemoved && handlerRemoved;
        }

        /// <summary>
        /// Removes the handler associated with the specified key.
        /// </summary>
        /// <remarks>If the specified key does not exist or no handler is associated with it, the method
        /// returns <see langword="false"/>.</remarks>
        /// <param name="key">The key identifying the handler to remove.</param>
        /// <returns><see langword="true"/> if a handler associated with the specified key was successfully removed;  otherwise,
        /// <see langword="false"/>.</returns>
        public bool Remove(string key)
        {
            if (_argumantKeyMap.TryGetValue(key, out var keys) && keys != null)
            {
                var handler = this.FirstOrDefault(h => h.Keys == keys);
                if (handler != null)
                {
                    return Remove(handler);
                }
            }

            return false;
        }
    }
}
