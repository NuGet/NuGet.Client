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

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstaller))]
    public class VsPackageInstaller : IVsPackageInstaller
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
            ISourceRepositoryProvider repoProvider = _sourceRepositoryProvider;

            // if the source is empty or null, just use everything
            if (!String.IsNullOrEmpty(source))
            {
                PreinstalledRepositoryProvider provider = new PreinstalledRepositoryProvider(ErrorHandler);
                provider.AddFromSource(GetSource(source));
            }

            List<PackageIdentity> packages = new List<PackageIdentity>() { new PackageIdentity(packageId, version) };

            Install(project, packages, repoProvider, false, ignoreDependencies);
        }

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
            PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler);
            repoProvider.AddFromRegistry(_sourceRepositoryProvider, keyName);

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

            PreinstalledRepositoryProvider repoProvider = new PreinstalledRepositoryProvider(ErrorHandler);
            repoProvider.AddFromExtension(_sourceRepositoryProvider, extensionId);
        }

        private void Install(Project project, List<PackageIdentity> packages, ISourceRepositoryProvider repoProvider, bool skipAssemblyReferences, bool ignoreDependencies)
        {
            try
            {
                // TODO: get the dependency behavior from settings
                DependencyBehavior depBehavior = DependencyBehavior.Lowest;

                if (ignoreDependencies)
                {
                    depBehavior = DependencyBehavior.Ignore;
                }

                bool includePrerelease = false;

                ResolutionContext resolution = new ResolutionContext(depBehavior, includePrerelease, false);

                NuGetPackageManager packageManager = new NuGetPackageManager(repoProvider, _settings, _solutionManager);

                // find the project
                NuGetProject nuGetProject = GetProject(project);

                // uninstall the package
                foreach (PackageIdentity package in packages)
                {
                    // HACK: We need to update nuget package manager to always take in an IEnumerable of primary repositories
                    packageManager.InstallPackageAsync(nuGetProject, package, resolution, _projectContext, repoProvider.GetRepositories().First()).Wait();
                }
            }
            finally
            {
                // TODO: log errors
            }
        }

        private NuGetProject GetProject(Project project)
        {
            return _solutionManager.GetNuGetProjects()
                    .Where(p => StringComparer.Ordinal.Equals(_solutionManager.GetNuGetProjectSafeName(p), project.UniqueName))
                    .SingleOrDefault();
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
    }
}
