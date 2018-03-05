using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    /// <summary>
    /// Provides a common facility for locating extensions
    /// </summary>
    public class ExtensionLocator : IExtensionLocator
    {
        private static readonly string ExtensionsDirectoryRoot = NuGetEnvironment.GetFolderPath(NuGetFolderPath.ExtensionsDirectory);

        private static readonly string CredentialProvidersDirectoryRoot = NuGetEnvironment.GetFolderPath(NuGetFolderPath.CredentialProvidersDirectory);

#if IS_CORECLR
        private static readonly string CredentialProviderPattern = "CredentialProvider*.dll";
#else
        private static readonly string CredentialProviderPattern = "CredentialProvider*.exe";
#endif

        public static readonly string ExtensionsEnvar = "NUGET_EXTENSIONS_PATH";

        public static readonly string CredentialProvidersEnvar = "NUGET_CREDENTIALPROVIDERS_PATH";

        /// <summary>
        /// Stores the base path to include as a location of extensions.
        /// </summary>
        private readonly string _basePath;

        /// <summary>
        /// Initializes a new instance of the ExtensionLocator class.
        /// </summary>
        /// <param name="basePath">A base path to include in the search for extensions.  This can be the directory where NuGet.exe is located.</param>
        public ExtensionLocator(string basePath)
        {
            _basePath = basePath;
        }

        /// <summary>
        /// Find paths to all extensions
        /// </summary>
        public IEnumerable<string> FindExtensions()
        {
            var customPaths = ReadPathsFromEnvar(ExtensionsEnvar);
            return FindAll(
                ExtensionsDirectoryRoot,
                customPaths,
                _basePath,
                "*.dll",
                "*Extensions.dll"
                );
        }

        /// <summary>
        /// Find paths to all credential providers
        /// </summary>
        public IEnumerable<string> FindCredentialProviders()
        {
            var customPaths = ReadPathsFromEnvar(CredentialProvidersEnvar);
            return FindAll(
                CredentialProvidersDirectoryRoot,
                customPaths,
                _basePath,
                CredentialProviderPattern,
                CredentialProviderPattern
                );
        }

        /// <summary>
        /// Helper method to locate extensions and credential providers.
        /// The following search locations will be checked in this order:
        /// 1) all directories under a path set by environment variable
        /// 2) all directories under a global path, e.g. %localappdata%\nuget\commands
        /// 3) the directory nuget.exe is located in
        /// </summary>
        /// <param name="globalRootDirectory">The global directory to search.  Will
        /// also check subdirectories.</param>
        /// <param name="customPaths">User-defined search paths.
        /// Will also check subdirectories.</param>
        /// <param name="basePath">A base path to search for extensions.  This is usually
        /// the directory of the running program like NuGet.exe.</param>
        /// <param name="assemblyPattern">The filename pattern to search for.</param>
        /// <param name="nugetDirectoryAssemblyPattern">The filename pattern to search for
        /// when looking in the nuget.exe directory. This is more restrictive so we do not
        /// accidentally pick up NuGet dlls.</param>
        /// <returns>An IEnumerable of paths to files matching the pattern in all searched
        /// directories.</returns>
        private static IEnumerable<string> FindAll(
            string globalRootDirectory,
            IEnumerable<string> customPaths,
            string basePath,
            string assemblyPattern,
            string nugetDirectoryAssemblyPattern)
        {
            var directories = new List<string>();

            // Add all directories from the environment variable if available.
            directories.AddRange(customPaths);

            // add the global root
            directories.Add(globalRootDirectory);

            var paths = new List<string>();
            foreach (var directory in directories.Where(Directory.Exists))
            {
                paths.AddRange(Directory.EnumerateFiles(directory, assemblyPattern, SearchOption.AllDirectories));
            }

            // Add the base path, usually the directory of nuget.exe, but be more careful since it contains
            // non-extension assemblies. Ideally we want to look for all files. However, using MEF to identify
            // imports results in assemblies being loaded and locked by our App Domain which could be slow,
            // might affect people's build systems and among other things breaks our build.
            // Consequently, we'll use a convention - only binaries ending in the name Extensions would be loaded.
            if (String.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
            {
                return paths;
            }

            paths.AddRange(Directory.EnumerateFiles(basePath, nugetDirectoryAssemblyPattern));

            return paths;
        }

        private static IEnumerable<string> ReadPathsFromEnvar(string key)
        {
            var result = new List<string>();
            var paths = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(paths))
            {
                result.AddRange(
                    paths.Split(new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries));
            }
            return result;
        }
    }
}
