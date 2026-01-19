using System.Reflection;

namespace Rheo.Storage.Utilities;

internal static class StoragePaths
{
    /// <summary>
    /// Gets the root name of the assembly.
    /// </summary>
    internal static string AssemblyRootName =>
        Assembly.GetExecutingAssembly().GetName().Name?.Split('.').First() ?? "Rheo.Storage";

    /// <summary>
    /// Gets the default local application data path for the assembly.
    /// </summary>
    internal static string DefaultLocalPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AssemblyRootName);

    /// <summary>
    /// Gets the location of the executable.
    /// </summary>
    internal static string DefaultRootPath => AppContext.BaseDirectory ?? DefaultLocalPath;
}

// Comment Icons: ⚠️✅❌💀
