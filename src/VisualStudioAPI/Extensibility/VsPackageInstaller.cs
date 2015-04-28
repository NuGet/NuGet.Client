extern alias Legacy;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using NuGet.VisualStudio.Resources;
using LegacyNuGet = Legacy.NuGet;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstaller))]
    public class VsPackageInstaller : IVsPackageInstaller2
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private ISettings _settings;
        private ISolutionManager _solutionManager;
        private INuGetProjectContext _projectContext;
        private IVsPackageInstallerServices _packageServices;

        [ImportingConstructor]
        public VsPackageInstaller(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager, IVsPackageInstallerServices packageServices)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _projectContext = new VSAPIProjectContext();
            _packageServices = packageServices;
        }

        public async Task InstallPackageAsync(Project project, IEnumerable<string> sources, string packageId, string versionSpec, bool ignoreDependencies, CancellationToken token)
        {
            var sourceProvider = GetSources(sources);

            VersionRange versionRange = VersionRange.All;

            if (!String.IsNullOrEmpty(versionSpec))
            {
                versionRange = VersionRange.Parse(versionSpec);
            }

            List<PackageDependency> toInstall = new List<PackageDependency>() { new PackageDependency(packageId, versionRange) };

            await InstallInternalAsync(project, toInstall, sourceProvider, false, ignoreDependencies, token);
        }

        public async Task InstallPackageAsync(IEnumerable<string> sources, Project project, string packageId, string version, bool ignoreDependencies, CancellationToken token)
        {
            var sourceProvider = GetSources(sources);

            NuGetVersion semVer = null;

            if (!String.IsNullOrEmpty(version))
            {
                NuGetVersion.TryParse(version, out semVer);
            }

            List<PackageIdentity> toInstall = new List<PackageIdentity>() { new PackageIdentity(packageId, semVer) };

            // Normalize the install folder for new installs (this only happens for IVsPackageInstaller2. IVsPackageInstaller keeps legacy behavior)
            VSAPIProjectContext projectContext = new VSAPIProjectContext(false, false, false);

            await InstallInternalAsync(project, toInstall, sourceProvider, projectContext, ignoreDependencies, token);
        }

        // Legacy methods
        public void InstallPackage(string source, Project project, string packageId, Version version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (version != null)
            {
                semVer = new NuGetVersion(version);
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await InstallPackageAsync(source, project, packageId, semVer, ignoreDependencies);
            });
        }

        public void InstallPackage(string source, Project project, string packageId, string version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (!String.IsNullOrEmpty(version))
            {
                NuGetVersion.TryParse(version, out semVer);
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await InstallPackageAsync(source, project, packageId, semVer, ignoreDependencies);
            });
        }

        private async Task InstallPackageAsync(string source, Project project, string packageId, NuGetVersion version, bool ignoreDependencies)
        {
            IEnumerable<string> sources = null;

            if (!String.IsNullOrEmpty(source))
            {
                sources = new string[] { source };
            }

            VersionRange versionRange = VersionRange.All;

            if (version != null)
            {
                versionRange = new VersionRange(version, true, version, true);
            }

            List<PackageIdentity> toInstall = new List<PackageIdentity>();
            toInstall.Add(new PackageIdentity(packageId, version));

            VSAPIProjectContext projectContext = new VSAPIProjectContext();

            await InstallInternalAsync(project, toInstall, GetSources(sources), projectContext, ignoreDependencies, CancellationToken.None);
        }

        public void InstallPackage(LegacyNuGet.IPackageRepository repository, Project project, string packageId, string version, bool ignoreDependencies, bool skipAssemblyReferences)
        {
            // It would be really difficult for anyone to use this method
            throw new NotSupportedException();
        }

        public void InstallPackagesFromRegistryRepository(string keyName, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions)
        {
            this.InstallPackagesFromRegistryRepository(keyName, isPreUnzipped, skipAssemblyReferences, ignoreDependencies: true, project: project, packageVersions: packageVersions);
        }

        public void InstallPackagesFromRegistryRepository(string keyName, bool isPreUnzipped, bool skipAssemblyReferences, bool ignoreDependencies, Project project, IDictionary<string, string> packageVersions)
        {
            if (String.IsNullOrEmpty(keyName))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "keyName");
            }

            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (packageVersions == null || !packageVersions.Any())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageVersions");
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                // create a repository provider with only the registry repository
                PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                repoProvider.AddFromRegistry(keyName);

                List<PackageIdentity> toInstall = GetIdentitiesFromDict(packageVersions);

                // Skip assembly references and disable binding redirections should be done together
                bool disableBindingRedirects = skipAssemblyReferences;

                VSAPIProjectContext projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);

                await InstallInternalAsync(project, toInstall, repoProvider, projectContext, ignoreDependencies, CancellationToken.None);
            });
        }

        public void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions)
        {
            InstallPackagesFromVSExtensionRepository(extensionId, isPreUnzipped, skipAssemblyReferences, ignoreDependencies: true, project: project, packageVersions: packageVersions);
        }

        public void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, bool ignoreDependencies, Project project, IDictionary<string, string> packageVersions)
        {
            if (String.IsNullOrEmpty(extensionId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "extensionId");
            }

            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (!packageVersions.Any())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageVersions");
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                repoProvider.AddFromExtension(_sourceRepositoryProvider, extensionId);

                List<PackageIdentity> toInstall = GetIdentitiesFromDict(packageVersions);

                // Skip assembly references and disable binding redirections should be done together
                bool disableBindingRedirects = skipAssemblyReferences;

                VSAPIProjectContext projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);

                await InstallInternalAsync(project, toInstall, repoProvider, projectContext, ignoreDependencies, CancellationToken.None);
            });
        }

        private static List<PackageIdentity> GetIdentitiesFromDict(IDictionary<string, string> packageVersions)
        {
            List<PackageIdentity> toInstall = new List<PackageIdentity>();

            // create identities
            foreach (var pair in packageVersions)
            {
                // TODO: versions can be null today, should this continue?
                NuGetVersion version = null;

                if (!String.IsNullOrEmpty(pair.Value))
                {
                    NuGetVersion.TryParse(pair.Value, out version);
                }

                toInstall.Add(new PackageIdentity(pair.Key, version));
            }

            return toInstall;
        }

        private Action<string> ErrorHandler
        {
            get
            {
                return (msg) =>
                    {
                        if (_projectContext != null)
                        {
                            _projectContext.Log(MessageLevel.Error, msg);
                        }
                    };
            }
        }

        /// <summary>
        /// Creates a repo provider for the given sources. If null is passed all sources will be returned.
        /// </summary>
        private ISourceRepositoryProvider GetSources(IEnumerable<string> sources)
        {
            ISourceRepositoryProvider provider = null;

            // add everything enabled if null
            if (sources == null)
            {
                // Use the default set of sources
                provider = _sourceRepositoryProvider;
            }
            else
            {
                // Create a custom source provider for the VS API install
                var customProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);

                // Create sources using the given set of sources
                foreach (string source in sources)
                {
                    customProvider.AddFromSource(GetSource(source));
                }

                provider = customProvider;
            }

            return provider;
        }

        /// <summary>
        /// Convert a source string to a SourceRepository. If one already exists that will be used.
        /// </summary>
        private SourceRepository GetSource(string source)
        {
            SourceRepository repo = _sourceRepositoryProvider.GetRepositories()
                .Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.PackageSource.Source, source)).FirstOrDefault();

            if (repo == null)
            {
                PackageSource newSource = new PackageSource(source);

                repo = _sourceRepositoryProvider.CreateRepository(newSource);
            }

            return repo;
        }

        internal async Task InstallInternalAsync(Project project, List<PackageDependency> packages, ISourceRepositoryProvider repoProvider, bool skipAssemblyReferences, bool ignoreDependencies, CancellationToken token)
        {
            foreach (var group in packages.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() > 1)
                {
                    // throw if a package id appears more than once
                    throw new InvalidOperationException(VsResources.InvalidPackageList);
                }
            }

            // find the latest package
            List<MetadataResource> metadataResources = new List<MetadataResource>();

            // create the resources for looking up the latest version
            foreach (var repo in repoProvider.GetRepositories())
            {
                MetadataResource resource = await repo.GetResourceAsync<MetadataResource>();
                if (resource != null)
                {
                    metadataResources.Add(resource);
                }
            }

            // find the highest version within the ranges
            var idToIdentity = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);

            foreach (var dep in packages)
            {
                NuGetVersion highestVersion = null;

                if (dep.VersionRange != null
                    && VersionComparer.Default.Equals(dep.VersionRange.MinVersion, dep.VersionRange.MaxVersion)
                    && dep.VersionRange.MinVersion != null)
                {
                    // this is a single version, not a range
                    highestVersion = dep.VersionRange.MinVersion;
                }
                else
                {
                    var tasks = new List<Task<IEnumerable<NuGetVersion>>>();

                    foreach (var resource in metadataResources)
                    {
                        tasks.Add(resource.GetVersions(dep.Id, token));
                    }

                    var versions = await Task.WhenAll(tasks.ToArray());

                    highestVersion = versions.SelectMany(v => v).Where(v => dep.VersionRange.Satisfies(v)).Max();
                }

                if (highestVersion == null)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, VsResources.UnknownPackage, dep.Id));
                }

                if (!idToIdentity.ContainsKey(dep.Id))
                {
                    idToIdentity.Add(dep.Id, new PackageIdentity(dep.Id, highestVersion));
                }
            }

            // Skip assembly references and disable binding redirections should be done together
            bool disableBindingRedirects = skipAssemblyReferences;

            VSAPIProjectContext projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);

            await InstallInternalAsync(project, idToIdentity.Values.ToList(), repoProvider, projectContext, ignoreDependencies, token);
        }

        /// <summary>
        /// Core install method. All installs from the VS API and template wizard end up here.
        /// </summary>
        internal async Task InstallInternalAsync(Project project, List<PackageIdentity> packages, ISourceRepositoryProvider repoProvider, VSAPIProjectContext projectContext, bool ignoreDependencies, CancellationToken token)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // store expanded node state
            IDictionary<string, ISet<VsHierarchyItem>> expandedNodes = await VsHierarchyHelper.GetAllExpandedNodesAsync(_solutionManager);

            try
            {
                DependencyBehavior depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;

                bool includePrerelease = false;

                ResolutionContext resolution = new ResolutionContext(depBehavior, includePrerelease, false);

                NuGetPackageManager packageManager = new NuGetPackageManager(repoProvider, _settings, _solutionManager);

                // find the project
                NuGetProject nuGetProject = PackageManagementHelpers.GetProject(_solutionManager, project, projectContext);

                // install the package
                foreach (PackageIdentity package in packages)
                {
                    if (package.Version == null)
                    {
                        if (!_packageServices.IsPackageInstalled(project, package.Id))
                        {
                            await packageManager.InstallPackageAsync(nuGetProject, package.Id, resolution, projectContext, repoProvider.GetRepositories(), Enumerable.Empty<SourceRepository>(), token);
                        }
                    }
                    else
                    {
                        if (!_packageServices.IsPackageInstalledEx(project, package.Id, package.Version.ToString()))
                        {
                            await packageManager.InstallPackageAsync(nuGetProject, package, resolution, projectContext, repoProvider.GetRepositories(), Enumerable.Empty<SourceRepository>(), token);
                        }
                    }
                }
            }
            finally
            {
                // collapse nodes
                await VsHierarchyHelper.CollapseAllNodesAsync(_solutionManager, expandedNodes);
            }
        }
    }
}
