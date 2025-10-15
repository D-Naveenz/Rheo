using Microsoft.Extensions.Configuration;
using Rheo.Extensions.Hosting.Commandline;
using System.Diagnostics.CodeAnalysis;

namespace Rheo.Extensions.Hosting.Argument
{
    /// <summary>
    /// Represents a handler for processing command-line arguments or configuration keys.
    /// </summary>
    /// <remarks>The <see cref="ArgumentHandler"/> class is designed to handle specific arguments or
    /// configuration keys based on the provided <see cref="ArgumentKeys"/> and associated processing logic. It supports
    /// optional descriptions, flags to define argument behavior, and a delegate function to execute when the argument
    /// is handled. This class ensures that the handler can process the argument by validating the configuration against
    /// the specified keys and flags. <para> The handler supports two constructor overloads, allowing the processing
    /// function to either include or exclude a <see cref="CancellationToken"/> parameter. If the <see
    /// cref="ArgumentType.ValueRequired"/> flag is set, it is automatically removed, as the handler does not process
    /// arguments with required values. </para> <para> This class is particularly useful in scenarios where dynamic
    /// argument handling is required, such as command-line tools or configuration-driven applications.
    /// </para></remarks>
    public class ArgumentHandler : IArgumentHandler, IEquatable<IArgumentHandler>
    {
        private readonly Func<CancellationToken, Task> _function;

        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public ArgumentHandler(
            ArgumentKeys keys,
            Func<Task> function,
            ArgumentType flags = default,
            string? description = null
        ) : this(keys, (token) => function(), flags, description)
        {
        }

        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public ArgumentHandler(
            ArgumentKeys keys,
            Func<CancellationToken, Task> function,
            ArgumentType flags = default,
            string? description = null
        )
        {
            // Remove ArgumentType.ValueRequired from flags because action does not take a parameter.
            if (flags.HasFlag(ArgumentType.ValueRequired))
            {
                flags &= ~ArgumentType.ValueRequired;
            }

            Keys = keys;
            Description = description;
            Flags = flags;
            _function = function;
        }

        public ArgumentKeys Keys { get; }

        public string? Description { get; set; }

        public ArgumentType Flags { get; }

        /// <summary>
        /// Executes the configured function asynchronously using the provided configuration and cancellation token.
        /// </summary>
        /// <remarks>This method invokes the function specified during the handler's initialization. 
        /// Ensure that the provided configuration is valid and that the cancellation token is used appropriately  to
        /// manage the operation's lifecycle.</remarks>
        /// <param name="configuration">The configuration settings to be used by the function.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public async Task HandleAsync(IConfiguration configuration, IConsoleService consoleService, CancellationToken cancellationToken)
        {
            bool canHandle = false;

            if (Flags.HasFlag(ArgumentType.Lunch))
            {
                // Check if the argument is meant to be a commandline (lunch) parameter
                if (configuration.TryGetValueFromConfiguration(Keys, out bool innerValue)
                    && !Flags.HasFlag(ArgumentType.ValueRequired))
                {
                    canHandle = innerValue;
                }
            }
            else
            {
                // Get the value from the commandline
                canHandle = consoleService.GetArgumentValue<bool>(Keys);
            }

            // Execute the function if the argument can be handled
            await (canHandle ? _function(cancellationToken) : Task.CompletedTask);
        }

        public bool Equals(IArgumentHandler? other)
        {
            if (other is null) return false;
            return Keys.Equals(other.Keys) && Flags == other.Flags;
        }

        public override string ToString() => $"Argument Handler for ({Keys}) | No value";

        public override bool Equals(object? obj) => Equals(obj as IArgumentHandler);

        public override int GetHashCode() => HashCode.Combine(Keys, Flags);
    }

    /// <summary>
    /// Represents a handler for processing arguments of a specific type, with support for validation, configuration,
    /// and asynchronous execution.
    /// </summary>
    /// <remarks>This class is designed to handle arguments identified by specific keys, validate their
    /// presence and value based on the specified flags, and execute a provided asynchronous function. It ensures that
    /// the <see cref="ArgumentType.ValueRequired"/> flag is set when the handler is initialized, as the handler
    /// requires a value to process. <para> The handler relies on the <see cref="Keys"/> property to identify the
    /// relevant configuration entries and uses the <see cref="_function"/> delegate to perform the processing logic.
    /// </para> <para> This class is intended for use in scenarios where arguments are dynamically resolved from a
    /// configuration source, and their processing involves asynchronous operations. </para></remarks>
    /// <typeparam name="T">The type of the argument value that this handler processes.</typeparam>
    public class ArgumentHandler<T> : IArgumentHandler, IEquatable<IArgumentHandler>
    {
        private readonly Func<T, CancellationToken, Task> _function;

        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public ArgumentHandler(
            ArgumentKeys keys,
            Func<T, Task> function,
            int valueCount = 1,
            ArgumentType flags = default,
            string? description = null
        ) : this(keys, (value, token) => function(value), valueCount, flags, description)
        {
        }

        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public ArgumentHandler(
            ArgumentKeys keys,
            Func<T, CancellationToken, Task> function,
            int valueCount = 1,
            ArgumentType flags = default,
            string? description = null
        )
        {
            // Ensure ArgumentType.ValueRequired is set because action takes a parameter.
            if (!flags.HasFlag(ArgumentType.ValueRequired))
            {
                flags |= ArgumentType.ValueRequired;
            }

            Keys = keys;
            Description = description;
            Flags = flags;
            _function = function;
            ValueCount = valueCount;
        }

        public ArgumentKeys Keys { get; }

        public string? Description { get; set; }

        public ArgumentType Flags { get; }

        public int ValueCount { get; }

        [RequiresUnreferencedCode(IArgumentHandler.TrimmingWarningMessage)]
        public async Task HandleAsync(IConfiguration configuration, IConsoleService consoleService, CancellationToken cancellationToken)
        {
            T? handlableValue = default;

            if (Flags.HasFlag(ArgumentType.Lunch))
            {
                // Check if the argument is meant to be a commandline (lunch) parameter
                if (configuration.TryGetValueFromConfiguration(Keys, out T? lunchValue)
                && Flags.HasFlag(ArgumentType.ValueRequired))
                {
                    handlableValue = lunchValue;
                }
            }
            else
            {
                // Get the value from the commandline
                handlableValue = consoleService.GetArgumentValue<T>(Keys);
            }

            // Execute the function if a valid value is found
            await (handlableValue != null ? _function(handlableValue, cancellationToken) : Task.CompletedTask);
        }

        public bool Equals(IArgumentHandler? other)
        {
            if (other is null) return false;
            return Keys == other.Keys && Flags == other.Flags;
        }

        public override string ToString() => $"Argument Handler for ({Keys}) | Value of type {typeof(T).Name}";

        public override bool Equals(object? obj) => Equals(obj as IArgumentHandler);

        public override int GetHashCode() => HashCode.Combine(Keys, Flags);
    }
}
