using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    public static class PluginDiscoveryUtility
    {
        public static Lazy<string> InternalPluginDiscoveryRoot { get; set; }

        private static string NuGetPluginsDirectory = "NuGetPlugins";

        public static string GetInternalPlugins()
        {
            return InternalPluginDiscoveryRoot?.Value ??
                GetNuGetPluginsDirectoryRelativeToNuGetAssembly(typeof(PluginDiscoveryUtility).GetTypeInfo().Assembly.Location); // NuGet.*.dll
        }

#if IS_DESKTOP
        /// <summary>
        /// Given Visual Studio 2017 MSBuild.exe path, return the NuGet plugins directory which is in CommonExtensions/NuGetPluginsDirectory
        /// </summary>
        /// <param name="msbuildExePath">The MsBuildExe path. Needs to be a valid path. file:// not supported.</param>
        /// <returns>The NuGet plugins directory, null if <paramref name="msbuildExePath"/> is null</returns>
        /// <remarks>The MSBuild.exe is in MSBuild\15.0\Bin\MsBuild.exe, the NuGetPlugins directory is in Common7\IDE\CommonExtensions\Microsoft\NuGetPlugins</remarks>
        public static string GetInternalPluginRelativeToMSBuildExe(string msbuildExePath)
        {
            var parentDirectory = "..";
            return !string.IsNullOrEmpty(msbuildExePath) ?
                PathUtility.GetAbsolutePath(
                    Path.GetDirectoryName(msbuildExePath),
                    Path.Combine(parentDirectory, parentDirectory, parentDirectory, "Common7", "CommonExtensions", "Microsoft", "NuGet", NuGetPluginsDirectory)
                    ) :
                null;
        }
#endif

        /// <summary>
        /// Given a NuGet assembly path, returns the NuGet plugins directory
        /// </summary>
        /// <param name="nugetAssemblyPath">The path to a NuGet assembly in CommonExtensions\NuGet</param>
        /// <returns>The NuGet plugins directory in CommonExtensions\NuGet\NuGetPlugins, null if the <paramref name="nugetAssemblyPath"/> is null</returns>
        public static string GetNuGetPluginsDirectoryRelativeToNuGetAssembly(string nugetAssemblyPath)
        {
            return !string.IsNullOrEmpty(nugetAssemblyPath) ?
                    Path.Combine(
                        Path.GetDirectoryName(nugetAssemblyPath),
                        NuGetPluginsDirectory
                        ) :
                    null;
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
