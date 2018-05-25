using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    public static class PluginDiscoveryUtility
    {
        public static Lazy<string> InternalPluginDiscoveryRoot { get; set; }

        public static string GetInternalPlugins()
        {
            var rootDirectory = InternalPluginDiscoveryRoot?.Value ?? System.Reflection.Assembly.GetEntryAssembly()?.Location;

            return rootDirectory ?? Path.GetDirectoryName(rootDirectory);
        }

        public static string GetNuGetHomePluginsPath()
        {
            var nuGetHome = NuGetEnvironment.GetFolderPath(NuGetFolderPath.NuGetHome);

            return Path.Combine(nuGetHome,
                "plugins",
#if IS_DESKTOP
                "netfx"
#else
                "netcore"
#endif
                );
        }

        public static IEnumerable<string> GetConventionBasedPlugins(IEnumerable<string> directories)
        {
            var paths = new List<string>();
            var existingDirectories = directories.Where(Directory.Exists);
            foreach (var directory in existingDirectories)
            {
                var pluginDirectories = Directory.GetDirectories(directory);

                foreach (var pluginDirectory in pluginDirectories)
                {
#if IS_DESKTOP
                    var expectedPluginName = Path.Combine(pluginDirectory, Path.GetFileName(pluginDirectory) + ".exe");
#else
                    var expectedPluginName = Path.Combine(pluginDirectory, Path.GetFileName(pluginDirectory) + ".dll");
#endif
                    if (File.Exists(expectedPluginName))
                    {
                        paths.Add(expectedPluginName);
                    }
                }
            }

            return paths;
        }
    }
}
