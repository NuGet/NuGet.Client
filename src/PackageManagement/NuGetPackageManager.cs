using NuGet.Client;
using NuGet.Configuration;
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

        private IPackageSourceProvider PackageSourceProvider { get; set; }
        private List<SourceRepository> SourceRepositories { get; set; }
        private IPackageResolver PackageResolver { get; set; }

        /// <summary>
        /// Creates a NuGetPackageManager for a given <param name="packageSourceProvider"></param> and <param name="packageResolver"></param>
        /// </summary>
        public NuGetPackageManager(IPackageSourceProvider packageSourceProvider, IPackageResolver packageResolver)
        {
            if(packageSourceProvider == null)
            {
                throw new ArgumentNullException("packageSourceProvider");
            }

            PackageResolver = packageResolver;
            PackageSourceProvider = packageSourceProvider;
            SourceRepositories = new List<SourceRepository>();

            // Refresh the package sources
            RefreshPackageSources();

            // Hook up event to refresh package sources when the package sources changed
            packageSourceProvider.PackageSourcesSaved += (sender, e) =>
            {
                RefreshPackageSources();
            };
        }

        /// <summary>
        /// Installs the latest version of the given <param name="packageId"></param> to NuGetProject <param name="project"></param>
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject project, string packageId)
        {
            // Step-1 : Get latest version for packageId
            var sourceRepository = FirstEnabledSourceRepository;
            var metadataResource = await sourceRepository.GetResource<MetadataResource>();
            var allVersions = await metadataResource.GetLatestVersions(new List<string>() { packageId });
            var latestVersion = allVersions.ToList().Max<NuGetVersion>();

            // Step-2 : Call InstallPackage(project, packageIdentity)
            await InstallPackageAsync(project, new PackageIdentity(packageId, latestVersion));
        }

        /// <summary>
        /// Installs given <param name="packageIdentity"></param> to NuGetProject <param name="project"></param>
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject project, PackageIdentity packageIdentity /* policies such as DependencyVersion, AllowPrerelease and so on */)
        {
            var packagesToInstall = new List<PackageIdentity>() { packageIdentity };
            // Step-1 : Get metadata resources using gatherer
            var sourceRepository = FirstEnabledSourceRepository;
            var dependencyInfoResource = await sourceRepository.GetResource<DepedencyInfoResource>();
            var packageDependencyInfo =
                await dependencyInfoResource.ResolvePackages(packagesToInstall, includePrerelease: false);

            // Step-2 : Call IPackageResolver.Resolve to get new list of installed packages
            var projectInstalledPackageReferences = project.GetInstalledPackages();
            var newListOfInstalledPackages = PackageResolver.Resolve(packagesToInstall, packageDependencyInfo, projectInstalledPackageReferences);

            // Step-3 : Get the list of package actions to perform, install/uninstall on the nugetproject 
            // based on newPackages obtained in Step-2 and project.GetInstalledPackages
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            var newPackagesToUninstall = oldListOfInstalledPackages.Where(p => !newListOfInstalledPackages.Contains(p));
            var newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));

            // Step-4 : For each package to be uninstalled, call into NuGetProject
            foreach(PackageIdentity newPackageToUninstall in newPackagesToUninstall)
            {
                ExecuteUninstall(project, newPackageToUninstall);
            }

            // Step-5 : For each package to be installed, call into NuGetProject
            foreach(PackageIdentity newPackageToInstall in newPackagesToInstall)
            {
                var packageStream = await PackageDownloader.GetPackage(sourceRepository, newPackageToInstall);
                ExecuteInstall(project, newPackageToInstall, packageStream);
            }
        }

        private void ExecuteInstall(NuGetProject nuGetProject, PackageIdentity packageIdentity, Stream packageStream)
        {
            var packageOperationEventArgs = new PackageOperationEventArgs(packageIdentity);
            if(PackageInstalling != null)
            {
                PackageInstalling(this, packageOperationEventArgs);
            }
            nuGetProject.InstallPackage(packageIdentity, packageStream);

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

        private void ExecuteUninstall(NuGetProject nuGetProject, PackageIdentity packageIdentity)
        {
            var packageOperationEventArgs = new PackageOperationEventArgs(packageIdentity);
            if (PackageUninstalling != null)
            {
                PackageUninstalling(this, packageOperationEventArgs);
            }
            nuGetProject.UninstallPackage(packageIdentity);

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

        // HACK: to always use the first source repository
        private SourceRepository FirstEnabledSourceRepository
        {
            get
            {
                return SourceRepositories[0];
            }
        }

        private void RefreshPackageSources()
        {
            SourceRepositories.Clear();
            foreach(var packageSource in PackageSourceProvider.LoadPackageSources().Where(s => s.IsEnabled))
            {
                SourceRepositories.Add(new SourceRepository(packageSource, null));
            }
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
