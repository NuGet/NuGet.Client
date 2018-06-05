using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    public static class PluginDiscoveryUtility
    {
        private static string NuGetPluginsDirectory = "NuGetPlugins";
        private static string ParentDirectory = "..";

        public static Lazy<string> InternalPluginDiscoveryRoot { get; set; }

        public static string GetInternalPlugins()
        {
#if IS_DESKTOP
            return InternalPluginDiscoveryRoot?.Value ??
                GetDesktopInternalPlugin(System.Reflection.Assembly.GetExecutingAssembly()?.Location); // NuGet.Protocol.dll would return null if called from unmanaged code
#else
            return InternalPluginDiscoveryRoot?.Value ??
                GetDotnetExeInternalPlugin(System.Reflection.Assembly.GetEntryAssembly()?.Location); // msbuild.dll - would return null if called from unmanaged code
#endif
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
            return msbuildExePath != null ?
                PathUtility.GetAbsolutePath(
                    Path.GetDirectoryName(msbuildExePath),
                    Path.Combine(ParentDirectory, ParentDirectory, ParentDirectory, "Common7", "CommonExtensions", "Microsoft", NuGetPluginsDirectory)
                    ) :
                null;
        }

        /// <summary>
        /// Given the NuGet assemblies directory, returns the NuGet plugins directory
        /// </summary>
        /// <param name="nuGetAssembliesDirectory">The NuGet assemblies directory in CommonExtensions\NuGet</param>
        /// <returns>The NuGet plugins directory in CommonExtensions\NuGetPlugins, null if the <paramref name="nuGetAssembliesDirectory"/> is null</returns>
        private static string GetDesktopInternalPlugin(string nuGetAssembliesDirectory)
        {
            return nuGetAssembliesDirectory != null ?
                    PathUtility.GetAbsolutePath(
                        Path.GetDirectoryName(nuGetAssembliesDirectory),
                        Path.Combine(ParentDirectory, NuGetPluginsDirectory)
                        ) :
                     null;
        }
#else
        /// <summary>
        /// Given the MsBuildDll directory, return the NuGetPlugins directory in dotnetexe
        /// MSBuild.dll and NuGet*.dlls are in the same directory in the SDK.
        /// </summary>
        /// <param name="msbuildAssemblyDirectory"></param>
        /// <returns></returns>
        private static string GetDotnetExeInternalPlugin(string msbuildAssemblyDirectory)
        {
            return msbuildAssemblyDirectory != null ?
                    Path.Combine( // else build the absolute path
                        Path.GetDirectoryName(msbuildAssemblyDirectory),
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
