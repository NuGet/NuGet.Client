extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using NuGet.PackageManagement;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using NuGet.PackagingCore;
using NuGet.Client;
using System.Diagnostics;
using NuGet.PackageManagement.VisualStudio;
using System.IO;
using System.Threading;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstaller))]
    public class VsPackageInstaller : IVsPackageInstaller2
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private ISettings _settings;
        private ISolutionManager _solutionManager;
        private INuGetProjectContext _projectContext;

        [ImportingConstructor]
        public VsPackageInstaller(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _projectContext = new VSAPIProjectContext();
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

            await InstallInternal(project, toInstall, sourceProvider, false, ignoreDependencies, token);
        }

        public async Task InstallPackageAsync(IEnumerable<string> sources, Project project, string packageId, string version, bool ignoreDependencies, CancellationToken token)
        {
            var sourceProvider = GetSources(sources);

            NuGetVersion semVer = null;

            if (!String.IsNullOrEmpty(version))
            {
                semVer = NuGetVersion.Parse(version);
            }

            List<PackageIdentity> toInstall = new List<PackageIdentity>() { new PackageIdentity(packageId, semVer) };

            await InstallInternal(project, toInstall, sourceProvider, false, ignoreDependencies, token);
        }


        public void InstallPackage(string source, Project project, string packageId, Version version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (version != null)
            {
                semVer = new NuGetVersion(version);
            }

            InstallPackage(source, project, packageId, semVer, ignoreDependencies);
        }

        public void InstallPackage(string source, Project project, string packageId, string version, bool ignoreDependencies)
        {
            NuGetVersion semVer = null;

            if (version != null)
            {
                semVer = new NuGetVersion(version);
            }

            InstallPackage(source, project, packageId, semVer, ignoreDependencies);
        }

        private void InstallPackage(string source, Project project, string packageId, NuGetVersion version, bool ignoreDependencies)
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

            // InstallInternal(project, )
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

            if (packageVersions == null || packageVersions.IsEmpty())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageVersions");
            }

            // create a repository provider with only the registry repository
            PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
            repoProvider.AddFromRegistry(keyName);

            List<PackageIdentity> packages = new List<PackageIdentity>();

            // create identities
            foreach (var pair in packageVersions)
            {
                // TODO: versions can be null today, should this continue?
                NuGetVersion version = null;

                if (!String.IsNullOrEmpty(pair.Value))
                {
                    NuGetVersion.TryParse(pair.Value, out version);
                }
            }
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

            if (packageVersions.IsEmpty())
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageVersions");
            }

            PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);
            repoProvider.AddFromExtension(_sourceRepositoryProvider, extensionId);
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
            PreinstalledRepositoryProvider provider = new PreinstalledRepositoryProvider(ErrorHandler, _sourceRepositoryProvider);

            IPackageSourceProvider sourceProvider = new PackageSourceProvider(_settings);

            PackageSource[] packageSources = sourceProvider.LoadPackageSources().ToArray();

            // add everything enabled if null
            if (sources == null)
            {
                foreach (var packageSource in packageSources)
                {
                    if (packageSource.IsEnabled)
                    {
                        foreach (string source in sources)
                        {
                            provider.AddFromSource(GetSource(source));
                        }
                    }
                }
            }
            else
            {
                // TODO: disallow disabled sources even if they were provided by the caller?
                foreach (string source in sources)
                {
                    provider.AddFromSource(GetSource(source));
                }
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

        internal async Task InstallInternal(Project project, List<PackageDependency> packages, ISourceRepositoryProvider repoProvider, bool skipAssemblyReferences, bool ignoreDependencies, CancellationToken token)
        {
            Dictionary<string, PackageIdentity> idToIdentity =  new Dictionary<string, PackageIdentity>();

            // add packages limited to a single version
            foreach (var dep in packages.Where(e => e.VersionRange.IsMaxInclusive && e.VersionRange.IsMinInclusive 
                && VersionComparer.Default.Equals(e.VersionRange.MinVersion, e.VersionRange.MaxVersion)))
            {
                idToIdentity.Add(dep.Id, new PackageIdentity(dep.Id, NuGetVersion.Parse(dep.VersionRange.MinVersion.ToNormalizedString())));
            }

            // find the latest package
            foreach (SourceRepository repo in repoProvider.GetRepositories())
            {
                foreach (string id in idToIdentity.Where(e => e.Value == null).Select(e => e.Key).ToArray())
                {
                    MetadataResource resource = await repo.GetResourceAsync<MetadataResource>();
                    var versions = await resource.GetVersions(id, token);

                    var dep = packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).SingleOrDefault();

                    // find the highest version
                    foreach (var version in versions.OrderByDescending(e => e, VersionComparer.Default))
                    {
                        if (dep.VersionRange == null || dep.VersionRange.Satisfies(version))
                        {
                            if (idToIdentity[id] == null || VersionComparer.VersionRelease.Compare(idToIdentity[id].Version, version) < 0)
                            {
                                idToIdentity[id] = new PackageIdentity(id, version);
                                break;
                            }
                        }
                    }
                }
            }

            foreach (var pair in idToIdentity)
            {
                if (pair.Value == null)
                {
                    // TODO: add message
                    throw new InvalidOperationException();
                }
            }

            await InstallInternal(project, idToIdentity.Values.ToList(), repoProvider, skipAssemblyReferences, ignoreDependencies, token);
        }

        internal async Task InstallInternal(Project project, List<PackageIdentity> packages, ISourceRepositoryProvider repoProvider, bool skipAssemblyReferences, bool ignoreDependencies, CancellationToken token)
        {
            try
            {
                // TODO: should this come from settings?
                DependencyBehavior depBehavior = DependencyBehavior.Lowest;

                if (ignoreDependencies)
                {
                    depBehavior = DependencyBehavior.Ignore;
                }

                bool includePrerelease = false;

                ResolutionContext resolution = new ResolutionContext(depBehavior, includePrerelease, false);

                var dir = _solutionManager.SolutionDirectory;

                NuGetPackageManager packageManager = new NuGetPackageManager(repoProvider, dir);

                // find the project
                NuGetProject nuGetProject = PackageManagementHelpers.GetProject(_solutionManager, project);

                VSAPIProjectContext projectContext = new VSAPIProjectContext();

                if (nuGetProject == null)
                {
                    VSNuGetProjectFactory factory = new VSNuGetProjectFactory(_solutionManager);
                    nuGetProject = factory.CreateNuGetProject(project, projectContext);
                }

                // install the package
                foreach (PackageIdentity package in packages)
                {
                    // HACK: We need to update nuget package manager to always take in an IEnumerable of primary repositories
                    await packageManager.InstallPackageAsync(nuGetProject, package, resolution, _projectContext, repoProvider.GetRepositories().First());
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
            }
            finally
            {
                // TODO: log errors
            }
        }
    }
}
