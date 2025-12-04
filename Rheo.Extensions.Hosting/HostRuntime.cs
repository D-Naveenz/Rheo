using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rheo.Extensions.Hosting
{
    /// <summary>
    /// Provides utility methods for building, running, and interacting with an application host.
    /// </summary>
    public static class HostRuntime
    {
        private static IHost? _host;
        private static readonly TaskCompletionSource<IHost> _buildTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Builds the application host and starts it asynchronously.
        /// </summary>
        /// <remarks>This method builds the application host using the provided <paramref name="builder"/>
        /// and starts it by invoking <see cref="IHost.RunAsync"/>. If an exception occurs during the build or run
        /// process, the exception is propagated to the caller.</remarks>
        /// <param name="builder">The <see cref="HostApplicationBuilder"/> used to configure and build the application host.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        public static async Task LaunchAsync(this HostApplicationBuilder builder)
        {
            try
            {
                _host = builder.Build();
                _buildTaskCompletionSource.SetResult(_host);
                await _host.RunAsync();
            }
            catch (Exception ex)
            {
                _buildTaskCompletionSource.SetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a required service of the specified type from the application's service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve. Must be a non-nullable reference type.</typeparam>
        /// <returns>The requested service of type <typeparamref name="T"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the host has not been built or initialized. Thrown if the requested service of type <typeparamref
        /// name="T"/> is not registered in the service provider.</exception>
        public static T GetService<T>() where T : notnull
        {
            if (_host is null)
                throw new InvalidOperationException("Host has not been built.");
            return _host.Services.GetRequiredService<T>();
        }

        /// <summary>
        /// Asynchronously retrieves a required service of the specified type from the application's service provider.
        /// </summary>
        /// <remarks>This method ensures that the host is fully built before attempting to retrieve the
        /// service.  The service must be registered in the application's dependency injection container.</remarks>
        /// <typeparam name="T">The type of the service to retrieve. This type must be non-nullable.</typeparam>
        /// <returns>A task that represents the asynchronous operation. The task result contains the requested service of type
        /// <typeparamref name="T"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the host has not been built.</exception>
        public static async Task<T> GetServiceAsync<T>() where T : notnull
        {
            await WaitForBuildAsync();
            if (_host is null)
                throw new InvalidOperationException("Host has not been built.");
            return _host.Services.GetRequiredService<T>();
        }

        /// <summary>
        /// Retrieves a service of the specified type that is associated with the given key.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve. Must be a non-nullable reference type.</typeparam>
        /// <param name="key">The key associated with the service to retrieve.</param>
        /// <returns>The service instance of type <typeparamref name="T"/> associated with the specified key.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the host has not been built. Thrown if no service of type <typeparamref name="T"/> is registered
        /// with the specified key.</exception>
        public static T GetKeyedService<T>(object key) where T : notnull
        {
            if (_host is null)
                throw new InvalidOperationException("Host has not been built.");
            return _host.Services.GetRequiredKeyedService<T>(key);
        }

        /// <summary>
        /// Retrieves a service of the specified type that is associated with the given key.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve. This type must not be nullable.</typeparam>
        /// <param name="key">The key associated with the service to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the service of type
        /// <typeparamref name="T"/>  associated with the specified key.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the host has not been built. Thrown if no service of type <typeparamref name="T"/> is registered
        /// with the specified key.</exception>
        public static async Task<T> GetKeyedServiceAsync<T>(object key) where T : notnull
        {
            await WaitForBuildAsync();
            if (_host is null)
                throw new InvalidOperationException("Host has not been built.");
            return _host.Services.GetRequiredKeyedService<T>(key);
        }

        private static async Task<IHost> WaitForBuildAsync() => await _buildTaskCompletionSource.Task.ConfigureAwait(false);
    }
}
