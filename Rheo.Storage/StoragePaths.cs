using System.Reflection;

namespace Rheo.Storage;

public static class StoragePaths
{
    /// <summary>
    /// Gets the root name of the assembly.
    /// </summary>
    public static string AssemblyRootName =>
        Assembly.GetExecutingAssembly().GetName().Name?.Split('.').First() ?? "Rheo.Storage";

    /// <summary>
    /// Gets the default local application data path for the assembly.
    /// </summary>
    public static string DefaultLocalPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AssemblyRootName);

    /// <summary>
    /// Gets the location of the executable.
    /// </summary>
    public static string DefaultRootPath => AppContext.BaseDirectory ?? DefaultLocalPath;
}
