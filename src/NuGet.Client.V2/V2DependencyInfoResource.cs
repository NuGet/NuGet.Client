using NuGet;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NuGet.PackagingCore;
using NuGet.Frameworks;
using System.Runtime.Versioning;

namespace NuGet.Client.V2
{

    public class V2DependencyInfoResource : DepedencyInfoResource
    {
        private readonly IPackageRepository V2Client;
        public V2DependencyInfoResource(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public V2DependencyInfoResource(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }
        public override Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(string packageId, Frameworks.NuGetFramework projectFramework, bool includePrerelease, System.Threading.CancellationToken token)
        {
            List<Tuple<string,IVersionSpec>> packageVersions = new List<Tuple<string,IVersionSpec>>();
            packageVersions.Add(new Tuple<string,IVersionSpec>(packageId,new VersionSpec()));
            return Task.Run(() => GetFlattenedDependencyTree(packageVersions, new List<PackageDependencyInfo>().AsEnumerable(), projectFramework, includePrerelease, token));
        }

        public override Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, Frameworks.NuGetFramework projectFramework, bool includePrerelease, System.Threading.CancellationToken token)
        {
            List<Tuple<string, IVersionSpec>> packageVersions = packages.Select(item => GetIdAndVersionSpec(item)).ToList();
            return Task.Run(() => GetFlattenedDependencyTree(packageVersions,new List<PackageDependencyInfo>().AsEnumerable(), projectFramework, includePrerelease, token));
        }

     

        #region PrivateHelpers
        private IEnumerable<PackageDependencyInfo> GetFlattenedDependencyTree(IEnumerable<Tuple<string, IVersionSpec>> packages, IEnumerable<PackageDependencyInfo> dependencyInfoList, Frameworks.NuGetFramework projectFramework, bool includePrerelease, System.Threading.CancellationToken token)
        {
               List<PackageDependencyInfo> packageDependencyInfo = dependencyInfoList.ToList();
                foreach (var package in packages)
                {
                       IEnumerable<IPackage> packageVersions = V2Client.FindPackages(package.Item1,package.Item2,includePrerelease,false);
                       foreach (var packageVersion in packageVersions)
                       {
                           PackageIdentity identity = new PackageIdentity(packageVersion.Id, NuGetVersion.Parse(packageVersion.Version.ToString()));
                           if (packageVersion.DependencySets != null && packageVersion.DependencySets.Count() > 0)
                           {
                               FrameworkReducer frameworkReducer = new FrameworkReducer();
                               NuGetFramework nearestFramework = frameworkReducer.GetNearest(projectFramework, packageVersion.DependencySets.Select(e => GetNuGetFramework(e)));
                               IEnumerable<PackageDependency> dependencies = packageVersion.DependencySets.Where(e => (GetNuGetFramework(e).Equals(nearestFramework))).FirstOrDefault().Dependencies;                               
                               packageDependencyInfo.Add(new PackageDependencyInfo(identity, dependencies.Select(item => GetNuGetPackagingCorePackageDependency(item))));
                               List<Tuple<string, IVersionSpec>> dependentPackages = dependencies.Select(item => GetIdAndVersionSpec(item)).ToList();
                               packageDependencyInfo.AddRange(GetFlattenedDependencyTree(dependentPackages, packageDependencyInfo, projectFramework, includePrerelease, token));
                           }
                           else
                           {
                               packageDependencyInfo.Add(new PackageDependencyInfo(identity, null));
                           }
                       }
                      
                }
            packageDependencyInfo =  packageDependencyInfo.Distinct().ToList();
            return packageDependencyInfo;
        }

        private Tuple<string,IVersionSpec> GetIdAndVersionSpec(PackageDependency item)
        {
            return new Tuple<string, IVersionSpec>(item.Id, item.VersionSpec);
        }
        private Tuple<string, IVersionSpec> GetIdAndVersionSpec(PackageIdentity item)
        {
            return new Tuple<string, IVersionSpec>(item.Id, new VersionSpec(new SemanticVersion(item.Version.ToNormalizedString())));
        }

        private NuGetFramework GetNuGetFramework(PackageDependencySet dependencySet)
        {
            NuGetFramework fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            return fxName;
        }

        private static NuGet.PackagingCore.PackageDependency GetNuGetPackagingCorePackageDependency(PackageDependency dependency)
        {
            string id = dependency.Id;
            VersionRange versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new NuGet.PackagingCore.PackageDependency(id, versionRange);
        }
        #endregion PrivateHelpers
    }
}
