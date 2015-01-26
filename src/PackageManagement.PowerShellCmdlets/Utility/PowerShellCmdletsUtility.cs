using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public static class PowerShellCmdletsUtility
    {
        /// <summary>
        /// Parse the NuGetVersion from string
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static NuGetVersion GetNuGetVersionFromString(string version)
        {
            NuGetVersion nVersion;
            if (version == null)
            {
                throw new ArgumentNullException();
            }
            else
            {
                bool success = NuGetVersion.TryParse(version, out nVersion);
                if (!success)
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture,
                        Resources.Cmdlet_FailToParseVersion, version));
                }
                return nVersion;
            }
        }

        /// <summary>
        /// Get latest package identity for specified package Id.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="project"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="sourceRepository"></param>
        /// <returns></returns>
        public static PackageIdentity GetLatestPackageIdentityForId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease)
        {
            string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
            List<string> targetFrameworks = new List<string>() { framework };
            SearchFilter searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;
            MetadataResource resource = sourceRepository.GetResource<MetadataResource>();
            PackageIdentity identity = null;

            try
            {
                Task<NuGetVersion> task = resource.GetLatestVersion(packageId, includePrerelease, false, CancellationToken.None);
                NuGetVersion latestVersion = task.Result;
                identity = new PackageIdentity(packageId, latestVersion);
            }
            catch (Exception)
            {
                if (identity == null)
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture,
                        Resources.UnknownPackage, packageId));
                }
            }

            return identity;
        }

        /// <summary>
        /// Get all versions for a specific package Id.
        /// </summary>
        /// <param name="sourceRepository"></param>
        /// <param name="packageId"></param>
        /// <param name="project"></param>
        /// <param name="includePrerelease"></param>
        /// <returns></returns>
        public static IEnumerable<NuGetVersion> GetAllVersionsForPackageId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease)
        {
            string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
            List<string> targetFrameworks = new List<string>() { framework };
            SearchFilter searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;
            PSSearchResource resource = sourceRepository.GetResource<PSSearchResource>();
            PSSearchMetadata result = null;
            IEnumerable<NuGetVersion> allVersions = Enumerable.Empty<NuGetVersion>();

            try
            {
                Task<IEnumerable<PSSearchMetadata>> task = resource.Search(packageId, searchfilter, 0, 30, CancellationToken.None);
                result = task.Result
                    .Where(p => string.Equals(p.Identity.Id, packageId, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                allVersions = result.Versions;
            }
            catch (Exception)
            {
                if (result == null || !allVersions.Any())
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture,
                        Resources.UnknownPackage, packageId));
                }

            }
            return result.Versions;
        }

        public static PackageIdentity GetSafePackageIdentityForId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease, NuGetVersion nugetVersion)
        {
            // Continue to get the latest version when above condition is not met.
            IEnumerable<NuGetVersion> allVersions = Enumerable.Empty<NuGetVersion>();
            var versionList = GetAllVersionsForPackageId(sourceRepository, packageId, project, includePrerelease);
            PackageIdentity identity = null;

            try
            {
                VersionRange spec = GetSafeRange(nugetVersion, includePrerelease);
                allVersions = versionList.Where(p => p < spec.MaxVersion && p >= spec.MinVersion);
                NuGetVersion version = allVersions.OrderByDescending(v => v).FirstOrDefault();
                identity = new PackageIdentity(packageId, version);
            }
            catch (Exception)
            {
                if (identity == null)
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture,
                        Resources.UnknownPackage, packageId));
                }
            }
            return identity;
        }

        /// <summary>
        /// The safe range is defined as the highest build and revision for a given major and minor version
        /// </summary>
        public static VersionRange GetSafeRange(NuGetVersion version, bool includePrerelease)
        {
            SemanticVersion max = new SemanticVersion(version.Major, version.Minor + 1, 0);
            NuGetVersion maxVersion = NuGetVersion.Parse(max.ToString());
            return new VersionRange(version, true, maxVersion, false, includePrerelease);
        }

        /// <summary>
        /// Get the update version for Dependent package, based on the specification of Highest, HighestMinor, HighestPatch and Lowest.
        /// </summary>
        /// <param name="sourceRepository"></param>
        /// <param name="identity"></param>
        /// <param name="project"></param>
        /// <param name="updateVersion"></param>
        /// <param name="includePrerelease"></param>
        /// <returns></returns>
        public static PackageIdentity GetUpdateForPackageByDependencyEnum(SourceRepository sourceRepository, PackageIdentity identity, NuGetProject project, DependencyBehavior updateVersion, bool includePrerelease)
        {
            if (identity == null)
            {
                return null;
            }

            IEnumerable<NuGetVersion> allVersions = GetAllVersionsForPackageId(sourceRepository, identity.Id, project, includePrerelease);
            // Find all versions that are higher than the package's current version
            allVersions = allVersions.Where(p => p > identity.Version).OrderByDescending(v => v);
            NuGetVersion nVersion = null;

            if (updateVersion == DependencyBehavior.Lowest)
            {
                nVersion = allVersions.LastOrDefault();
            }
            else if (updateVersion == DependencyBehavior.Highest)
            {
                nVersion = allVersions.FirstOrDefault();
            }
            else if (updateVersion == DependencyBehavior.HighestPatch)
            {
                var groups = from p in allVersions
                             group p by new { p.Version.Major, p.Version.Minor } into g
                             orderby g.Key.Major, g.Key.Minor
                             select g;
                nVersion = (from p in groups.First()
                            orderby p.Version descending
                            select p).FirstOrDefault();
            }
            else if (updateVersion == DependencyBehavior.HighestMinor)
            {
                var groups = from p in allVersions
                             group p by new { p.Version.Major } into g
                             orderby g.Key.Major
                             select g;
                nVersion = (from p in groups.First()
                            orderby p.Version descending
                            select p).FirstOrDefault();
            }

            if (nVersion != null)
            {
                return new PackageIdentity(identity.Id, nVersion);
            }
            else
            {
                throw new ArgumentOutOfRangeException("updateVersion");
            }
        }
    }
}
