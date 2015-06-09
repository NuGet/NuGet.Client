// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Implementation.Resources;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Provides functionality for installing packages already on disk from an installer (MSI or VSIX).
    /// </summary>
    internal class PreinstalledPackageInstaller
    {
        private const string RegistryKeyRoot = @"SOFTWARE\NuGet\Repository";
        //private readonly IVsWebsiteHandler _websiteHandler;
        private readonly IVsPackageInstallerServices _packageServices;
        //private readonly IVsCommonOperations _vsCommonOperations;
        private readonly ISolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceProvider;
        private readonly VsPackageInstaller _installer;

        public Action<string> InfoHandler { get; set; }

        public PreinstalledPackageInstaller(
            IVsPackageInstallerServices packageServices,
            ISolutionManager solutionManager,
            Configuration.ISettings settings,
            ISourceRepositoryProvider sourceProvider,
            VsPackageInstaller installer)
        {
            //_websiteHandler = websiteHandler;
            _packageServices = packageServices;
            //_vsCommonOperations = vsCommonOperations;
            _solutionManager = solutionManager;
            _sourceProvider = sourceProvider;
            _installer = installer;
        }

        /// <summary>
        /// Gets the folder location where packages have been laid down for the specified extension.
        /// </summary>
        /// <param name="extensionId">The installed extension.</param>
        /// <param name="vsExtensionManager">The VS Extension manager instance.</param>
        /// <param name="throwingErrorHandler">
        /// An error handler that accepts the error message string and then throws
        /// the appropriate exception.
        /// </param>
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
        /// <param name="throwingErrorHandler">
        /// An error handler that accepts the error message string and then throws
        /// the appropriate exception.
        /// </param>
        /// <returns>The absolute path to the packages folder specified in the registry.</returns>
        internal string GetRegistryRepositoryPath(string keyName, IEnumerable<IRegistryKey> registryKeys, Action<string> throwingErrorHandler)
        {
            IRegistryKey repositoryKey = null;
            string repositoryValue = null;

            // When pulling the repository from the registry, use CurrentUser first, falling back onto LocalMachine
            registryKeys = registryKeys ??
                           new[]
                               {
                                   new RegistryKeyWrapper(Registry.CurrentUser),
                                   new RegistryKeyWrapper(Registry.LocalMachine)
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

        /// <summary>
        /// Installs one or more packages into the specified project.
        /// </summary>
        /// <param name="packageInstaller">The package installer service that performs the actual package installation.</param>
        /// <param name="project">The target project for installation.</param>
        /// <param name="configuration">
        /// The packages to install, where to install them from, and additional options for
        /// their installation.
        /// </param>
        /// <param name="repositorySettings">The repository settings for the packages being installed.</param>
        /// <param name="warningHandler">
        /// An action that accepts a warning message and presents it to the user, allowing
        /// execution to continue.
        /// </param>
        /// <param name="errorHandler">
        /// An action that accepts an error message and presents it to the user, allowing
        /// execution to continue.
        /// </param>
        internal async Task PerformPackageInstallAsync(
            IVsPackageInstaller packageInstaller,
            Project project,
            PreinstalledPackageConfiguration configuration,
            Action<string> warningHandler,
            Action<string> errorHandler)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string repositoryPath = configuration.RepositoryPath;
            var failedPackageErrors = new List<string>();

            IPackageRepository repository = configuration.IsPreunzipped
                ? new UnzippedPackageRepository(repositoryPath)
                : (IPackageRepository)new LazyLocalPackageRepository(repositoryPath);

            PreinstalledRepositoryProvider repos = new PreinstalledRepositoryProvider(errorHandler, _sourceProvider);
            repos.AddFromRepository(repository);

            // store expanded node state
            IDictionary<string, ISet<VsHierarchyItem>> expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync(_solutionManager);

            foreach (var package in configuration.Packages)
            {
                // Does the project already have this package installed?
                if (_packageServices.IsPackageInstalled(project, package.Id))
                {
                    // If so, is it the right version?
                    if (!_packageServices.IsPackageInstalledEx(project, package.Id, package.Version.ToNormalizedString()))
                    {
                        // No? Raise a warning (likely written to the Output window) and ignore this package.
                        warningHandler(String.Format(VsResources.PreinstalledPackages_VersionConflict, package.Id, package.Version));
                    }
                    // Yes? Just silently ignore this package!
                }
                else
                {
                    try
                    {
                        if (InfoHandler != null)
                        {
                            InfoHandler(String.Format(CultureInfo.CurrentCulture, VsResources.PreinstalledPackages_PackageInstallStatus, package.Id, package.Version));
                        }

                        List<PackageIdentity> toInstall = new List<PackageIdentity>();
                        toInstall.Add(new PackageIdentity(package.Id, package.Version));

                        // Skip assembly references and disable binding redirections should be done together
                        bool disableBindingRedirects = package.SkipAssemblyReferences;

                        VSAPIProjectContext projectContext = new VSAPIProjectContext(package.SkipAssemblyReferences, disableBindingRedirects);

                        // Old templates have hardcoded non-normalized paths
                        projectContext.PackageExtractionContext.UseLegacyPackageInstallPath = true;

                        // This runs from the UI thread
                        await _installer.InstallInternalAsync(project, toInstall, repos, projectContext, package.IgnoreDependencies, CancellationToken.None);
                    }
                    catch (InvalidOperationException exception)
                    {
                        failedPackageErrors.Add(package.Id + "." + package.Version + " : " + exception.Message);
                    }
                    catch (AggregateException aggregateEx)
                    {
                        var ex = aggregateEx.Flatten().InnerExceptions.FirstOrDefault();
                        if (ex is InvalidOperationException)
                        {
                            failedPackageErrors.Add(package.Id + "." + package.Version + " : " + ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            if (failedPackageErrors.Any())
            {
                var errorString = new StringBuilder();
                errorString.AppendFormat(VsResources.PreinstalledPackages_FailedToInstallPackage, repositoryPath);
                errorString.AppendLine();
                errorString.AppendLine();
                errorString.Append(String.Join(Environment.NewLine, failedPackageErrors));

                errorHandler(errorString.ToString());
            }

            // RepositorySettings = null in unit tests
            if (EnvDTEProjectUtility.IsWebSite(project))
            {
                CreateRefreshFilesInBin(
                    project,
                    repositoryPath,
                    configuration.Packages.Where(p => p.SkipAssemblyReferences));

                CopyNativeBinariesToBin(project, repositoryPath, configuration.Packages);
            }

            // collapse nodes
            await VsHierarchyUtility.CollapseAllNodesAsync(_solutionManager, expandedNodes);
        }

        /// <summary>
        /// For Website projects, adds necessary "refresh" files in the bin folder for added references.
        /// </summary>
        /// <param name="project">The target Website project.</param>
        /// <param name="repositoryPath">The local repository path.</param>
        /// <param name="packageInfos">The packages that were installed.</param>
        private void CreateRefreshFilesInBin(Project project, string repositoryPath, IEnumerable<PreinstalledPackageInfo> packageInfos)
        {
            IEnumerable<PackageIdentity> packageNames = packageInfos.Select(pi => new PackageIdentity(pi.Id, pi.Version));
            AddRefreshFilesForReferences(project, repositoryPath, packageNames);
        }

        /// <summary>
        /// Adds refresh files to the specified project for all assemblies references belonging to the packages
        /// specified by packageNames.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="repositoryPath">The file system pointing to 'packages' folder under the solution.</param>
        /// <param name="packageNames">The package names.</param>
        private void AddRefreshFilesForReferences(Project project, string repositoryPath, IEnumerable<PackageIdentity> packageNames)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (repositoryPath == null)
            {
                throw new ArgumentNullException("repositoryPath");
            }

            if (!packageNames.Any())
            {
                return;
            }

            VSAPIProjectContext context = new VSAPIProjectContext(skipAssemblyReferences: true, bindingRedirectsDisabled: true);
            WebSiteProjectSystem projectSystem = new WebSiteProjectSystem(project, context);

            foreach (var packageName in packageNames)
            {
                string packagePath = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", packageName.Id, packageName.Version);

                DirectoryInfo packageFolder = new DirectoryInfo(Path.Combine(repositoryPath, packagePath));

                PackageFolderReader reader = new PackageFolderReader(packageFolder);

                var frameworkGroups = reader.GetReferenceItems();

                var groups = reader.GetReferenceItems();

                var fwComparer = new NuGetFrameworkFullComparer();
                FrameworkReducer reducer = new FrameworkReducer();
                NuGetFramework targetGroupFramework = reducer.GetNearest(projectSystem.TargetFramework, groups.Select(e => e.TargetFramework));

                if (targetGroupFramework != null)
                {
                    var refGroup = groups.Where(e => fwComparer.Equals(targetGroupFramework, e.TargetFramework)).FirstOrDefault();

                    foreach (string refItem in refGroup.Items)
                    {
                        string sourcePath = Path.Combine(packageFolder.FullName, refItem.Replace('/', Path.DirectorySeparatorChar));

                        // create one refresh file for each assembly reference, as per required by Website projects
                        // projectSystem.CreateRefreshFile(assemblyPath);
                        RefreshFileUtility.CreateRefreshFile(projectSystem, sourcePath);
                    }
                }
            }
        }

        /// <summary>
        /// By convention, we copy all files under the NativeBinaries folder under package root to the bin folder of
        /// the Website.
        /// </summary>
        /// <param name="project">The target Website project.</param>
        /// <param name="repositoryPath">The local repository path.</param>
        /// <param name="packageInfos">The packages that were installed.</param>
        private void CopyNativeBinariesToBin(Project project, string repositoryPath, IEnumerable<PreinstalledPackageInfo> packageInfos)
        {
            VSAPIProjectContext context = new VSAPIProjectContext();
            VSMSBuildNuGetProjectSystem projectSystem = new VSMSBuildNuGetProjectSystem(project, context);

            foreach (var packageInfo in packageInfos)
            {
                string packagePath = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", packageInfo.Id, packageInfo.Version);

                CopyNativeBinaries(projectSystem, repositoryPath,
                    Path.Combine(repositoryPath, packagePath));
            }
        }

        private void CopyNativeBinaries(VSMSBuildNuGetProjectSystem projectSystem, string repositoryPath, string packagePath)
        {
            const string nativeBinariesFolder = "NativeBinaries";
            const string binFolder = "bin";

            DirectoryInfo nativeBinariesPath = new DirectoryInfo(Path.Combine(packagePath, nativeBinariesFolder));
            if (nativeBinariesPath.Exists)
            {
                FileInfo[] nativeFiles = nativeBinariesPath.GetFiles("*.*", SearchOption.AllDirectories);
                foreach (FileInfo file in nativeFiles)
                {
                    string targetPath = Path.Combine(binFolder, file.FullName.Substring(nativeBinariesPath.FullName.Length + 1)); // skip over NativeBinaries/ word
                    using (Stream stream = file.OpenRead())
                    {
                        projectSystem.AddFile(targetPath, stream);
                    }
                }
            }
        }
    }
}
