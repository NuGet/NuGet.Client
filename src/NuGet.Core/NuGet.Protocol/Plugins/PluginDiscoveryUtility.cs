using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    public static class PluginDiscoveryUtility
    {
        private static readonly string NuGetPluginsDirectory = "NuGetPlugins";

        public static Lazy<string> InternalPluginDiscoveryRoot { get; set; }

        public static string GetInternalPlugins()
        {
#if IS_DESKTOP
            return InternalPluginDiscoveryRoot?.Value ?? GetDesktopInternalPlugin();
#else
            return InternalPluginDiscoveryRoot?.Value ?? GetNetCoreInternalPlugin();
#endif
        }

#if IS_DESKTOP
        private static string GetDesktopInternalPlugin()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()?.Location != null ? // nuget.protocol.dll - would return null if called from unmanaged code
                    PathUtility.GetAbsolutePath( // else build the absolute path
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly()?.Location),
                        Path.Combine("..", NuGetPluginsDirectory)
                        ) :
                     null;
        }
#else
        private static string GetNetCoreInternalPlugin()
        {
            return System.Reflection.Assembly.GetEntryAssembly()?.Location != null ? // msbuild.dll - would return null if called from unmanaged code
                    Path.Combine( // else build the absolute path
                        Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location),
                        NuGetPluginsDirectory
                        ) :
                    null;
        }
#endif
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
