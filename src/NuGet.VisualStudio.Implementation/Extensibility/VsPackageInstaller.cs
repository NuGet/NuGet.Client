// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using NuGet.VisualStudio.Implementation.Resources;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstaller))]
    public class VsPackageInstaller : IVsPackageInstaller
    {
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly Configuration.ISettings _settings;
        private readonly ISolutionManager _solutionManager;
        private readonly INuGetProjectContext _projectContext;
        private readonly IVsPackageInstallerServices _packageServices;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;

        private JoinableTaskFactory PumpingJTF { get; }

        [ImportingConstructor]
        public VsPackageInstaller(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager,
            IVsPackageInstallerServices packageServices,
            IDeleteOnRestartManager deleteOnRestartManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _projectContext = new VSAPIProjectContext();
            _packageServices = packageServices;
            _deleteOnRestartManager = deleteOnRestartManager;
            PumpingJTF = new PumpingJTF(ThreadHelper.JoinableTaskContext);
        }

        public void InstallPackage(string source, Project project, string packageId, Version version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (version != null)
            {
                semVer = new NuGetVersion(version);
            }

            PumpingJTF.Run(() => InstallPackageAsync(source, project, packageId, semVer, ignoreDependencies));
        }

        public void InstallPackage(string source, Project project, string packageId, string version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (!String.IsNullOrEmpty(version))
            {
                NuGetVersion.TryParse(version, out semVer);
            }

            PumpingJTF.Run(() => InstallPackageAsync(source, project, packageId, semVer, ignoreDependencies));
        }

        private Task InstallPackageAsync(string source, Project project, string packageId, NuGetVersion version, bool ignoreDependencies)
        {
            IEnumerable<string> sources = null;

            if (!String.IsNullOrEmpty(source))
            {
                sources = new[] { source };
            }

            VersionRange versionRange = VersionRange.All;

            if (version != null)
            {
                versionRange = new VersionRange(version, true, version, true);
            }

            List<PackageIdentity> toInstall = new List<PackageIdentity>();
            toInstall.Add(new PackageIdentity(packageId, version));

            VSAPIProjectContext projectContext = new VSAPIProjectContext();

            return InstallInternalAsync(project, toInstall, GetSources(sources), projectContext, ignoreDependencies, CancellationToken.None);
        }

        public void InstallPackage(IPackageRepository repository, Project project, string packageId, string version, bool ignoreDependencies, bool skipAssemblyReferences)
        {
            // It would be really difficult for anyone to use this method
            throw new NotSupportedException();
        }

        public void InstallPackagesFromRegistryRepository(string keyName, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions)
        {
            InstallPackagesFromRegistryRepository(keyName, isPreUnzipped, skipAssemblyReferences, ignoreDependencies: true, project: project, packageVersions: packageVersions);
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

            if (packageVersions == null
                || !packageVersions.Any())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageVersions");
            }

            PumpingJTF.Run(() =>
                {
                    // create a repository provider with only the registry repository
                    PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                    repoProvider.AddFromRegistry(keyName, isPreUnzipped);

                    List<PackageIdentity> toInstall = GetIdentitiesFromDict(packageVersions);

                    // Skip assembly references and disable binding redirections should be done together
                    bool disableBindingRedirects = skipAssemblyReferences;

                    VSAPIProjectContext projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);

                    return InstallInternalAsync(project, toInstall, repoProvider, projectContext, ignoreDependencies, CancellationToken.None);
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

            PumpingJTF.Run(() =>
                {
                    PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                    repoProvider.AddFromExtension(_sourceRepositoryProvider, extensionId);

                    List<PackageIdentity> toInstall = GetIdentitiesFromDict(packageVersions);

                    // Skip assembly references and disable binding redirections should be done together
                    bool disableBindingRedirects = skipAssemblyReferences;

                    VSAPIProjectContext projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);

                    return InstallInternalAsync(project, toInstall, repoProvider, projectContext, ignoreDependencies, CancellationToken.None);
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
                return msg =>
                    {
                        if (_projectContext != null)
                        {
                            _projectContext.Log(ProjectManagement.MessageLevel.Error, msg);
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
                var newSource = new Configuration.PackageSource(source);

                repo = _sourceRepositoryProvider.CreateRepository(newSource);
            }

            return repo;
        }

        internal async Task InstallInternalAsync(Project project, List<Packaging.Core.PackageDependency> packages, ISourceRepositoryProvider repoProvider, bool skipAssemblyReferences, bool ignoreDependencies, CancellationToken token)
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
            // Go off the UI thread. This may be called from the UI thread. Only switch to the UI thread where necessary
            // This method installs multiple packages and can likely take more than a few secs
            // So, go off the UI thread explicitly to improve responsiveness
            await TaskScheduler.Default;

            // store expanded node state
            IDictionary<string, ISet<VsHierarchyItem>> expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync(_solutionManager);

            try
            {
                DependencyBehavior depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;

                bool includePrerelease = false;

                ResolutionContext resolution = new ResolutionContext(depBehavior, includePrerelease, false, VersionConstraints.None);

                NuGetPackageManager packageManager =
                    new NuGetPackageManager(
                        repoProvider,
                        _settings,
                        _solutionManager,
                        _deleteOnRestartManager);

                // find the project
                NuGetProject nuGetProject = await PackageManagementHelpers.GetProjectAsync(_solutionManager, project, projectContext);

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
                await VsHierarchyUtility.CollapseAllNodesAsync(_solutionManager, expandedNodes);
            }
        }
    }
}
