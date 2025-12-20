using Rheo.Storage.DefinitionsBuilder.Properties;
using System.Reflection;
using System.Text;

namespace Rheo.Storage.DefinitionsBuilder.Settings
{
    internal static class Configuration
    {
        private const string DataPath = "Data";

        public const string FILEDEF_PACKAGE_NAME = "filedefs";

        /// <summary>
        /// Gets the absolute path to the assets directory.
        /// </summary>
        public static string AssetsPath => Path.GetFullPath(DataPath);

        /// <summary>
        /// Gets the file system path to the TrID directory within the assets folder.
        /// </summary>
        public static string TridLocation => Path.Combine(AssetsPath, "TrID");

        /// <summary>
        /// Gets the name of the currently executing assembly's main executable file.
        /// </summary>
        /// <remarks>If the assembly name cannot be determined, the value defaults to
        /// "defbuild.exe".</remarks>
        public static string ExeName
        {
            get
            {
                var name = Assembly.GetExecutingAssembly().GetName().Name;
                return string.IsNullOrEmpty(name)? name + ".exe" : "defbuild.exe";
            }
        }

        /// <summary>
        /// Gets the version of the current executing assembly in the format "Major.Minor".
        /// </summary>
        public static string Version
        {
            get
            {
                var versionInfo = Assembly.GetExecutingAssembly().GetName().Version;
                return versionInfo != null ? string.Concat(versionInfo.Major, '.', versionInfo.Minor) : "1.0";
            }
        }

        /// <summary>
        /// Gets the product name defined in the assembly's <see cref="AssemblyProductAttribute"/>.
        /// </summary>
        /// <remarks>If the assembly does not specify a product name, the value defaults to "Rheo
        /// Definitions Builder".</remarks>
        public static string ProductName
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var attributes = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length > 0)
                {
                    var productAttribute = (AssemblyProductAttribute)attributes[0];
                    return productAttribute.Product;
                }

                // Fallback product name if attribute is not found
                return "Definitions Builder";
            }
        }

        /// <summary>
        /// Gets a <see cref="MemoryStream"/> containing the embedded FIGlet font data in UTF-8 encoding.
        /// </summary>
        /// <remarks>The returned stream contains the basic FIGlet font, which can be used for rendering
        /// ASCII art text.  The caller is responsible for disposing the returned <see cref="MemoryStream"/> when it is
        /// no longer needed.</remarks>
        public static MemoryStream FigletFont
        {
            get
            {
                var encoding = Encoding.UTF8;
                byte[] bytes = encoding.GetBytes(Resources.FigletBasicFont);
                return new MemoryStream(bytes);
            }
        }
    }
}
