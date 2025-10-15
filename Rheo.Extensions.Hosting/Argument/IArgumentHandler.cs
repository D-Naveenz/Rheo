using Microsoft.Extensions.Configuration;
using Rheo.Extensions.Hosting.Commandline;
using System.Diagnostics.CodeAnalysis;

namespace Rheo.Extensions.Hosting.Argument
{
    public interface IArgumentHandler
    {
        internal const string TrimmingWarningMessage = "In case the type is non-primitive, the trimmer cannot statically analyze the object's type so its members may be trimmed.";

        /// <summary>
        /// Gets the collection of argument keys associated with the current instance.
        /// </summary>
        ArgumentKeys Keys { get; }

        /// <summary>
        /// Gets or sets the description associated with the object.
        /// </summary>
        string? Description { get; set; }

        /// <summary>
        /// Gets the flags that specify the behavior or characteristics of the argument.
        /// </summary>
        ArgumentType Flags { get; }

        /// <summary>
        /// Handles the execution of an asynchronous operation using the provided configuration and console service.
        /// </summary>
        /// <remarks>This method requires unreferenced code and may not work correctly in scenarios where
        /// code trimming is applied.</remarks>
        /// <param name="configuration">The configuration settings used to control the operation.</param>
        /// <param name="consoleService">The console service used for input/output operations during the execution.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [RequiresUnreferencedCode(TrimmingWarningMessage)]
        Task HandleAsync(IConfiguration configuration, IConsoleService consoleService, CancellationToken cancellationToken);
    }
}
