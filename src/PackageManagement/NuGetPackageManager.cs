using NuGet.Client;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// NuGetPackageManager orchestrates a nuget package operation such as an install or uninstall
    /// It is to be called by various NuGet Clients including the custom third-party ones
    /// </summary>
    public class NuGetPackageManager
    {
        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageOperationEventArgs> PackageInstalling;
        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageOperationEventArgs> PackageInstalled;
        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageOperationEventArgs> PackageUninstalling;
        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageOperationEventArgs> PackageUninstalled;

        private SourceRepositoryProvider SourceRepositoryProvider { get; set; }
        private IPackageResolver PackageResolver { get; set; }

        /// <summary>
        /// Creates a NuGetPackageManager for a given <param name="sourceRepositoryProvider"></param> and <param name="packageResolver"></param>
        /// </summary>
        public NuGetPackageManager(SourceRepositoryProvider sourceRepositoryProvider, IPackageResolver packageResolver)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException("sourceRepositoryProvider");
            }

            PackageResolver = packageResolver;
            SourceRepositoryProvider = sourceRepositoryProvider;
        }

        /// <summary>
        /// Installs the latest version of the given <param name="packageId"></param> to NuGetProject <param name="nuGetProject"></param>
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject nuGetProject, string packageId, ResolutionContext resolutionContext, IExecutionContext executionContext)
        {
            // HACK: Need to all the sourceRepositories much like it is done for DependencyInfoResource and DownloadResource
            // Step-1 : Get latest version for packageId
            var sourceRepository = SourceRepositoryProvider.GetRepositories().First();
            var metadataResource = sourceRepository.GetResource<MetadataResource>();
            if(metadataResource != null)
            {
                var allVersions = await metadataResource.GetLatestVersions(new List<string>() { packageId });
                var latestVersion = allVersions.ToList().Max<NuGetVersion>();

                // Step-2 : Call InstallPackage(project, packageIdentity)
                await InstallPackageAsync(nuGetProject, new PackageIdentity(packageId, latestVersion), resolutionContext, executionContext);
            }
        }

        /// <summary>
        /// Installs given <param name="packageIdentity"></param> to NuGetProject <param name="nuGetProject"></param>
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity, ResolutionContext resolutionContext, IExecutionContext executionContext)
        {
            var packagesToInstall = new List<PackageIdentity>() { packageIdentity };
            // Step-1 : Get metadata resources using gatherer
            var availablePackageDependencyInfoWithSourceSet = await GatherPackageDependencyInfo(packageIdentity, nuGetProject.TargetFramework);

            // Step-2 : Call IPackageResolver.Resolve to get new list of installed packages
            var projectInstalledPackageReferences = nuGetProject.GetInstalledPackages();
            var newListOfInstalledPackages = PackageResolver.Resolve(packagesToInstall, availablePackageDependencyInfoWithSourceSet.Keys, projectInstalledPackageReferences);

            // Step-3 : Get the list of package actions to perform, install/uninstall on the nugetproject 
            // based on newPackages obtained in Step-2 and project.GetInstalledPackages
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            var newPackagesToUninstall = oldListOfInstalledPackages.Where(p => !newListOfInstalledPackages.Contains(p));
            var newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));

            // Step-4 : For each package to be uninstalled, call into NuGetProject
            foreach(PackageIdentity newPackageToUninstall in newPackagesToUninstall)
            {
                ExecuteUninstall(nuGetProject, newPackageToUninstall, executionContext);
            }

            // Step-5 : For each package to be installed, call into NuGetProject
            foreach(PackageIdentity newPackageToInstall in newPackagesToInstall)
            {
                var fakePkgDepInfo = new PackageDependencyInfo(newPackageToInstall.Id, newPackageToInstall.Version);
                SourceRepository sourceRepository;
                if(!availablePackageDependencyInfoWithSourceSet.TryGetValue(fakePkgDepInfo, out sourceRepository))
                {
                    throw new InvalidOperationException("Package cannot be installed because the source repository is not known??!!");
                }
                var packageStream = await PackageDownloader.GetPackageStream(sourceRepository, newPackageToInstall);
                ExecuteInstall(nuGetProject, newPackageToInstall, packageStream, executionContext);
            }
        }

        private void ExecuteInstall(NuGetProject nuGetProject, PackageIdentity packageIdentity, Stream packageStream, IExecutionContext executionContext)
        {
            var packageOperationEventArgs = new PackageOperationEventArgs(packageIdentity);
            if(PackageInstalling != null)
            {
                PackageInstalling(this, packageOperationEventArgs);
            }
            nuGetProject.InstallPackage(packageIdentity, packageStream, executionContext);

            // TODO: Consider using CancelEventArgs instead of a regular EventArgs??
            //if (packageOperationEventArgs.Cancel)
            //{
            //    return;
            //}

            if(PackageInstalled != null)
            {
                PackageInstalled(this, packageOperationEventArgs);
            }
        }

        private void ExecuteUninstall(NuGetProject nuGetProject, PackageIdentity packageIdentity, IExecutionContext executionContext)
        {
            var packageOperationEventArgs = new PackageOperationEventArgs(packageIdentity);
            if (PackageUninstalling != null)
            {
                PackageUninstalling(this, packageOperationEventArgs);
            }
            nuGetProject.UninstallPackage(packageIdentity, executionContext);

            // TODO: Consider using CancelEventArgs instead of a regular EventArgs??
            //if (packageOperationEventArgs.Cancel)
            //{
            //    return;
            //}

            if (PackageUninstalled != null)
            {
                PackageUninstalled(this, packageOperationEventArgs);
            }
        }

        private async Task<IDictionary<PackageDependencyInfo, SourceRepository>> GatherPackageDependencyInfo(PackageIdentity packageIdentity, NuGetFramework targetFramework)
        {
            // get a distinct set of packages from all repos
            var packageDependencyInfoSet = new Dictionary<PackageDependencyInfo, SourceRepository>(PackageDependencyInfo.Comparer);

            // find all needed packages from online
            foreach (var sourceRepository in SourceRepositoryProvider.GetRepositories())
            {
                // get the resolver data resource
                var dependencyInfoResource = sourceRepository.GetResource<DepedencyInfoResource>();

                // resources can always be null
                if (dependencyInfoResource != null)
                {
                    var packageDependencyInfo = await dependencyInfoResource.ResolvePackages(new PackageIdentity[] { packageIdentity }, targetFramework, true);

                    foreach (var pkgDepInfo in packageDependencyInfo)
                    {
                        if(!packageDependencyInfoSet.ContainsKey(pkgDepInfo))
                        {
                            packageDependencyInfoSet.Add(pkgDepInfo, sourceRepository);
                        }
                    }
                }
            }

            return packageDependencyInfoSet;
        }
    }

    /// <summary>
    /// The event args class used in raising package operation events
    /// </summary>
    public  class PackageOperationEventArgs : EventArgs
    {
        PackageIdentity PackageIdentity { get; set; }
        /// <summary>
        /// Creates a package operation event args object for given <param name="packageIdentity"></param>
        /// </summary>
        public PackageOperationEventArgs(PackageIdentity packageIdentity)
        {
            PackageIdentity = packageIdentity;
        }
    }
}
