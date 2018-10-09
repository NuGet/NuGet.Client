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
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using NuGet.VisualStudio.Implementation.Resources;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstaller))]
    [Export(typeof(IVsPackageInstaller2))]
    public class VsPackageInstaller : IVsPackageInstaller2
    {
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly Configuration.ISettings _settings;
        private readonly IVsSolutionManager _solutionManager;
        private readonly IVsPackageInstallerServices _packageServices;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        private readonly Lazy<INuGetProjectContext> _projectContext;
        private bool _isCPSJTFLoaded;

        // Reason it's lazy<object> is because we don't want to load any CPS assemblies until
        // we're really going to use any of CPS api. Which is why we also don't use nameof or typeof apis.
        [Import("Microsoft.VisualStudio.ProjectSystem.IProjectServiceAccessor")]
        private Lazy<object> ProjectServiceAccessor { get; set; }

        private JoinableTaskFactory PumpingJTF { get; set; }

        [ImportingConstructor]
        public VsPackageInstaller(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            IVsSolutionManager solutionManager,
            IVsPackageInstallerServices packageServices,
            IDeleteOnRestartManager deleteOnRestartManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _packageServices = packageServices;
            _deleteOnRestartManager = deleteOnRestartManager;
            _isCPSJTFLoaded = false;

            _projectContext = new Lazy<INuGetProjectContext>(() => {
                var projectContext = new VSAPIProjectContext();

                var logger = new LoggerAdapter(projectContext);
                projectContext.PackageExtractionContext = new PackageExtractionContext(
                    PackageSaveMode.Defaultv2,
                    PackageExtractionBehavior.XmlDocFileSaveMode,
                    ClientPolicyContext.GetClientPolicy(_settings, logger),
                    logger);

                return projectContext;
            });

            PumpingJTF = new PumpingJTF(NuGetUIThreadHelper.JoinableTaskFactory);
        }

        private void RunJTFWithCorrectContext(Project project, Func<Task> asyncTask)
        {
            if (!_isCPSJTFLoaded)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var vsHierarchy = VsHierarchyUtility.ToVsHierarchy(project);
                    if (vsHierarchy != null &&
                        VsHierarchyUtility.IsCPSCapabilityComplaint(vsHierarchy))
                    {
                        // Lazy load the CPS enabled JoinableTaskFactory for the UI.
                        NuGetUIThreadHelper.SetJoinableTaskFactoryFromService(ProjectServiceAccessor.Value as IProjectServiceAccessor);

                        PumpingJTF = new PumpingJTF(NuGetUIThreadHelper.JoinableTaskFactory);
                        _isCPSJTFLoaded = true;
                    }
                });
            }

            PumpingJTF.Run(asyncTask);
        }

        public void InstallLatestPackage(
            string source,
            Project project,
            string packageId,
            bool includePrerelease,
            bool ignoreDependencies)
        {
            RunJTFWithCorrectContext(project, () => InstallPackageAsync(
                source,
                project,
                packageId,
                version: null,
                includePrerelease: includePrerelease,
                ignoreDependencies: ignoreDependencies));
        }

        public void InstallPackage(string source, Project project, string packageId, Version version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (version != null)
            {
                semVer = new NuGetVersion(version);
            }

            RunJTFWithCorrectContext(project, () => InstallPackageAsync(
                source,
                project,
                packageId,
                version: semVer,
                includePrerelease: false,
                ignoreDependencies: ignoreDependencies));
        }

        public void InstallPackage(string source, Project project, string packageId, string version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (!string.IsNullOrEmpty(version))
            {
                NuGetVersion.TryParse(version, out semVer);
            }

            RunJTFWithCorrectContext(project, () => InstallPackageAsync(
                source,
                project,
                packageId,
                version: semVer,
                includePrerelease: false,
                ignoreDependencies: ignoreDependencies));
        }

        private Task InstallPackageAsync(string source, Project project, string packageId, NuGetVersion version, bool includePrerelease, bool ignoreDependencies)
        {
            IEnumerable<string> sources = null;

            if (!string.IsNullOrEmpty(source) &&
                !StringComparer.OrdinalIgnoreCase.Equals("All", source)) // "All" was supported in V2
            {
                sources = new[] { source };
            }

            var versionRange = VersionRange.All;

            if (version != null)
            {
                versionRange = new VersionRange(version, true, version, true);
            }

            var toInstall = new List<PackageIdentity>
            {
                new PackageIdentity(packageId, version)
            };

            var projectContext = new VSAPIProjectContext();
            var logger = new LoggerAdapter(projectContext);

            projectContext.PackageExtractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv2,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                ClientPolicyContext.GetClientPolicy(_settings, logger),
                logger);

            return InstallInternalAsync(project, toInstall, GetSources(sources), projectContext, includePrerelease, ignoreDependencies, CancellationToken.None);
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
            if (string.IsNullOrEmpty(keyName))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(keyName));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (packageVersions == null
                || !packageVersions.Any())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageVersions));
            }

            RunJTFWithCorrectContext(project, async () =>
                {
                    // HACK !!! : This is a hack for PCL projects which send isPreUnzipped = true, but their package source 
                    // (located at C:\Program Files (x86)\Microsoft SDKs\NuGetPackages) follows the V3
                    // folder version format.
                    if (isPreUnzipped)
                    {
                        var isProjectJsonProject = await EnvDTEProjectUtility.HasBuildIntegratedConfig(project);
                        isPreUnzipped = isProjectJsonProject ? false : isPreUnzipped;
                    }

                    // create a repository provider with only the registry repository
                    var repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                    repoProvider.AddFromRegistry(keyName, isPreUnzipped);

                    var toInstall = GetIdentitiesFromDict(packageVersions);

                    // Skip assembly references and disable binding redirections should be done together
                    var disableBindingRedirects = skipAssemblyReferences;

                    var projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);
                    var logger = new LoggerAdapter(projectContext);

                    projectContext.PackageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings, logger),
                        logger);

                    await InstallInternalAsync(
                        project,
                        toInstall,
                        repoProvider,
                        projectContext,
                        includePrerelease: false,
                        ignoreDependencies: ignoreDependencies,
                        token: CancellationToken.None);
                });
        }

        public void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions)
        {
            InstallPackagesFromVSExtensionRepository(
                extensionId,
                isPreUnzipped,
                skipAssemblyReferences,
                ignoreDependencies: true,
                project: project,
                packageVersions: packageVersions);
        }

        public void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, bool ignoreDependencies, Project project, IDictionary<string, string> packageVersions)
        {
            if (string.IsNullOrEmpty(extensionId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(extensionId));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!packageVersions.Any())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageVersions));
            }

            RunJTFWithCorrectContext(project, () =>
                {
                    var repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
                    repoProvider.AddFromExtension(_sourceRepositoryProvider, extensionId);

                    var toInstall = GetIdentitiesFromDict(packageVersions);

                    // Skip assembly references and disable binding redirections should be done together
                    var disableBindingRedirects = skipAssemblyReferences;

                    var projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);
                    var logger = new LoggerAdapter(projectContext);
                    projectContext.PackageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings, logger),
                        logger);

                    return InstallInternalAsync(
                        project,
                        toInstall,
                        repoProvider,
                        projectContext,
                        includePrerelease: false,
                        ignoreDependencies: ignoreDependencies,
                        token: CancellationToken.None);
                });
        }

        private static List<PackageIdentity> GetIdentitiesFromDict(IDictionary<string, string> packageVersions)
        {
            var toInstall = new List<PackageIdentity>();

            // create identities
            foreach (var pair in packageVersions)
            {
                // TODO: versions can be null today, should this continue?
                NuGetVersion version = null;

                if (!string.IsNullOrEmpty(pair.Value))
                {
                    NuGetVersion.TryParse(pair.Value, out version);
                }

                toInstall.Add(new PackageIdentity(pair.Key, version));
            }

            return toInstall;
        }

        private Action<string> ErrorHandler => msg =>
        {
            _projectContext.Value.Log(ProjectManagement.MessageLevel.Error, msg);
        };

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
                foreach (var source in sources)
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
            var repo = _sourceRepositoryProvider.GetRepositories()
                .Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.PackageSource.Source, source)).FirstOrDefault();

            if (repo == null)
            {
                Uri result;
                if (!Uri.TryCreate(source, UriKind.Absolute, out result))
                {
                    throw new ArgumentException(
                        string.Format(VsResources.InvalidSource, source),
                        nameof(source));
                }

                var newSource = new Configuration.PackageSource(source);

                repo = _sourceRepositoryProvider.CreateRepository(newSource);
            }

            return repo;
        }

        internal async Task InstallInternalAsync(
            Project project,
            List<Packaging.Core.PackageDependency> packages,
            ISourceRepositoryProvider repoProvider,
            bool skipAssemblyReferences,
            bool ignoreDependencies,
            CancellationToken token)
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
            var metadataResources = new List<MetadataResource>();

            // create the resources for looking up the latest version
            foreach (var repo in repoProvider.GetRepositories())
            {
                var resource = await repo.GetResourceAsync<MetadataResource>();
                if (resource != null)
                {
                    metadataResources.Add(resource);
                }
            }

            // find the highest version within the ranges
            var idToIdentity = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);

            using (var sourceCacheContext = new SourceCacheContext())
            {
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
                            tasks.Add(resource.GetVersions(dep.Id, sourceCacheContext, NullLogger.Instance, token));
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
            }

            // Skip assembly references and disable binding redirections should be done together
            var disableBindingRedirects = skipAssemblyReferences;

            var projectContext = new VSAPIProjectContext(skipAssemblyReferences, disableBindingRedirects);
            var logger = new LoggerAdapter(projectContext);

            projectContext.PackageExtractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv2,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                ClientPolicyContext.GetClientPolicy(_settings, logger),
                logger);

            await InstallInternalAsync(
                project,
                idToIdentity.Values.ToList(),
                repoProvider,
                projectContext,
                includePrerelease: false,
                ignoreDependencies: ignoreDependencies,
                token: token);
        }

        /// <summary>
        /// Internal install method. All installs from the VS API and template wizard end up here.
        /// </summary>
        internal async Task InstallInternalAsync(
            Project project,
            List<PackageIdentity> packages,
            ISourceRepositoryProvider repoProvider,
            VSAPIProjectContext projectContext,
            bool includePrerelease,
            bool ignoreDependencies,
            CancellationToken token)
        {
            // Go off the UI thread. This may be called from the UI thread. Only switch to the UI thread where necessary
            // This method installs multiple packages and can likely take more than a few secs
            // So, go off the UI thread explicitly to improve responsiveness
            await TaskScheduler.Default;

            var gatherCache = new GatherCache();
            var sources = repoProvider.GetRepositories().ToList();

            // store expanded node state
            var expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync(_solutionManager);

            try
            {
                var depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;

                var packageManager = CreatePackageManager(repoProvider);

                // find the project
                var nuGetProject = await _solutionManager.GetOrCreateProjectAsync(project, projectContext);

                // install the package
                foreach (var package in packages)
                {
                    // Check if the package is already installed
                    if (package.Version == null)
                    {
                        if (_packageServices.IsPackageInstalled(project, package.Id))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (_packageServices.IsPackageInstalledEx(project, package.Id, package.Version.ToString()))
                        {
                            continue;
                        }
                    }

                    // Perform the install
                    await InstallInternalCoreAsync(
                        packageManager,
                        gatherCache,
                        nuGetProject,
                        package,
                        sources,
                        projectContext,
                        includePrerelease,
                        ignoreDependencies,
                        token);
                }
            }
            finally
            {
                // collapse nodes
                await VsHierarchyUtility.CollapseAllNodesAsync(_solutionManager, expandedNodes);
            }
        }

        /// <summary>
        /// Core install method. All installs from the VS API and template wizard end up here.
        /// This does not check for already installed packages
        /// </summary>
        internal async Task InstallInternalCoreAsync(
            NuGetPackageManager packageManager,
            GatherCache gatherCache,
            NuGetProject nuGetProject,
            PackageIdentity package,
            IEnumerable<SourceRepository> sources,
            VSAPIProjectContext projectContext,
            bool includePrerelease,
            bool ignoreDependencies,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            var depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;

            using (var sourceCacheContext = new SourceCacheContext())
            {
                ResolutionContext resolution = new ResolutionContext(
                    depBehavior,
                    includePrerelease,
                    includeUnlisted: false,
                    versionConstraints: VersionConstraints.None,
                    gatherCache: gatherCache,
                    sourceCacheContext: sourceCacheContext);

                // install the package
                if (package.Version == null)
                {
                    await packageManager.InstallPackageAsync(nuGetProject, package.Id, resolution, projectContext, sources, Enumerable.Empty<SourceRepository>(), token);
                }
                else
                {
                    await packageManager.InstallPackageAsync(nuGetProject, package, resolution, projectContext, sources, Enumerable.Empty<SourceRepository>(), token);
                }
            }
        }

        /// <summary>
        /// Create a new NuGetPackageManager with the IVsPackageInstaller settings.
        /// </summary>
        internal NuGetPackageManager CreatePackageManager(ISourceRepositoryProvider repoProvider)
        {
            return new NuGetPackageManager(
                repoProvider,
                _settings,
                _solutionManager,
                _deleteOnRestartManager);
        }
    }
}
