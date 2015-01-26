using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.VisualStudio.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    internal sealed class PreinstalledRepositoryProvider : ISourceRepositoryProvider
    {
        private const string RegistryKeyRoot = @"SOFTWARE\NuGet\Repository";
        private List<SourceRepository> _repositories;
        private readonly Action<string> _errorHandler;

        public PreinstalledRepositoryProvider(Action<string> errorHandler)
        {
            _repositories = new List<SourceRepository>();
            _errorHandler = errorHandler;
        }

        public void AddFromRegistry(ISourceRepositoryProvider provider, string keyName)
        {
            string path = GetRegistryRepositoryPath(keyName, null, _errorHandler);

            PackageSource source = new PackageSource(path);

            _repositories.Add(provider.CreateRepository(source));
        }

        public void AddFromExtension(ISourceRepositoryProvider provider, string extensionId)
        {
            string path = GetExtensionRepositoryPath(extensionId, null, _errorHandler);

            PackageSource source = new PackageSource(path);

            _repositories.Add(provider.CreateRepository(source));
        }

        public void AddFromSource(SourceRepository repo)
        {
            _repositories.Add(repo);
        }

        public SourceRepository CreateRepository(PackageSource source)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        public IPackageSourceProvider PackageSourceProvider
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the folder location where packages have been laid down for the specified extension.
        /// </summary>
        /// <param name="extensionId">The installed extension.</param>
        /// <param name="vsExtensionManager">The VS Extension manager instance.</param>
        /// <param name="throwingErrorHandler">An error handler that accepts the error message string and then throws the appropriate exception.</param>
        /// <returns>The absolute path to the extension's packages folder.</returns>
        internal string GetExtensionRepositoryPath(string extensionId, object vsExtensionManager, Action<string> throwingErrorHandler)
        {
            var extensionManagerShim = new ExtensionManagerShim(vsExtensionManager, throwingErrorHandler);
            string installPath;

            if (!extensionManagerShim.TryGetExtensionInstallPath(extensionId, out installPath))
            {
                throwingErrorHandler(String.Format(VsResources.PreinstalledPackages_InvalidExtensionId,
                    extensionId));
                Debug.Fail("The throwingErrorHandler did not throw");
            }

            return Path.Combine(installPath, "Packages");
        }

        /// <summary>
        /// Gets the folder location where packages have been laid down in a registry-specified location.
        /// </summary>
        /// <param name="keyName">The registry key name that specifies the packages location.</param>
        /// <param name="registryKeys">The optional list of parent registry keys to look in (used for unit tests).</param>
        /// <param name="throwingErrorHandler">An error handler that accepts the error message string and then throws the appropriate exception.</param>
        /// <returns>The absolute path to the packages folder specified in the registry.</returns>
        internal string GetRegistryRepositoryPath(string keyName, IEnumerable<IRegistryKey> registryKeys, Action<string> throwingErrorHandler)
        {
            IRegistryKey repositoryKey = null;
            string repositoryValue = null;

            // When pulling the repository from the registry, use CurrentUser first, falling back onto LocalMachine
            registryKeys = registryKeys ??
                new[] {
                            new RegistryKeyWrapper(Microsoft.Win32.Registry.CurrentUser),
                            new RegistryKeyWrapper(Microsoft.Win32.Registry.LocalMachine)
                      };

            // Find the first registry key that supplies the necessary subkey/value
            foreach (var registryKey in registryKeys)
            {
                repositoryKey = registryKey.OpenSubKey(RegistryKeyRoot);

                if (repositoryKey != null)
                {
                    repositoryValue = repositoryKey.GetValue(keyName) as string;

                    if (!String.IsNullOrEmpty(repositoryValue))
                    {
                        break;
                    }

                    repositoryKey.Close();
                }
            }

            if (repositoryKey == null)
            {
                throwingErrorHandler(String.Format(VsResources.PreinstalledPackages_RegistryKeyError, RegistryKeyRoot));
                Debug.Fail("throwingErrorHandler did not throw");
            }

            if (String.IsNullOrEmpty(repositoryValue))
            {
                throwingErrorHandler(String.Format(VsResources.PreinstalledPackages_InvalidRegistryValue, keyName, RegistryKeyRoot));
                Debug.Fail("throwingErrorHandler did not throw");
            }

            // Ensure a trailing slash so that the path always gets read as a directory
            repositoryValue = PathUtility.EnsureTrailingSlash(repositoryValue);

            return Path.GetDirectoryName(repositoryValue);
        }
    }
}
