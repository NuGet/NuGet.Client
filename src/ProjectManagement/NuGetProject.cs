using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System.Collections.Generic;
using System.IO;

namespace NuGet.ProjectManagement
{
    public abstract class NuGetProject
    {
        // Is the concept of TargetFramework core enough in NuGet to be here?
        // Or is it specific to Visual Studio projects like a net45 or projectk library?
        public abstract NuGetFramework TargetFramework { get; }
        // TODO: Consider adding CancellationToken here
        /// <summary>
        /// This installs a package into the NuGetProject using the packageStream passed in
        /// </summary>
        /// <returns>Returns false if the package was already present in the NuGetProject. On successful installation, returns true</returns>
        public abstract bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, IExecutionContext executionContext);
        /// <summary>
        /// This uninstalls the package from the NuGetProject, if found
        /// </summary>
        /// <returns>Returns false if the package was not found. On successful uninstallation, returns true</returns>
        public abstract bool UninstallPackage(PackageIdentity packageIdentity, IExecutionContext executionContext);
        /// <summary>
        /// GetInstalledPackages will be used by Dependency Resolver and more
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<PackageReference> GetInstalledPackages();
    }
}
