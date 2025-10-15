using Microsoft.Extensions.Configuration;
using Rheo.Extensions.Hosting.Argument;
using Rheo.Extensions.Hosting.Commandline;
using System.Diagnostics.CodeAnalysis;

namespace Rheo.Extensions.Hosting.Activation
{
    internal class ActivationService(
        IEnumerable<IActivationHandler> activationHandlers,
        IConfiguration configuration,
        IConsoleBuilder consoleBuilder,
        ArgumentHandlerCollection commandlineArgCollection
        ) : IActivationService
    {
        [RequiresUnreferencedCode("Calls IconTeX.Host.Commandline.IConsoleBuilder.BuildArguments(ArgumentHandlerCollection)")]
        public async Task ActivateAsync(string[] activationArgs)
        {
            // Build the argument collection from the provided args
            consoleBuilder.BuildArguments(commandlineArgCollection);

            await HandleActivationAsync(configuration);
        }

        private async Task HandleActivationAsync(IConfiguration configs)
        {
            foreach (var handler in activationHandlers)
            {
                if (handler.CanHandle(configs))
                {
                    await handler.HandleAsync(configs, default);
                }
            }
        }
    }
}
