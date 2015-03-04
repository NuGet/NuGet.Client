using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;
using NuGet.Versioning;

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
        /// Get project's target frameworks
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetProjectTargetFrameworks(NuGetProject project)
        {
            List<string> frameworks = new List<string>();
            NuGetFramework nugetFramework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
            if (nugetFramework != null)
            {
                string framework = nugetFramework.ToString();
                frameworks.Add(framework);
            }
            return frameworks;
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
            IEnumerable<string> targetFrameworks = GetProjectTargetFrameworks(project);
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

        /// <summary>
        /// Return the latest version for package Id.
        /// </summary>
        /// <param name="sourceRepository"></param>
        /// <param name="packageId"></param>
        /// <param name="project"></param>
        /// <param name="includePrerelease"></param>
        /// <returns></returns>
        public static NuGetVersion GetLastestVersionForPackageId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease)
        {
            var versionList = GetAllVersionsForPackageId(sourceRepository, packageId, project, includePrerelease);
            return versionList.OrderByDescending(v => v).FirstOrDefault();
        }

        /// <summary>
        /// Get safe update version for installed package identity. Used for Update-Package -Safe.
        /// </summary>
        /// <param name="sourceRepository"></param>
        /// <param name="identity"></param>
        /// <param name="project"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="nugetVersion"></param>
        /// <returns></returns>
        public static PackageIdentity GetSafeUpdateForPackageIdentity(SourceRepository sourceRepository, PackageIdentity identity, NuGetProject project, bool includePrerelease, NuGetVersion nugetVersion)
        {
            IEnumerable<NuGetVersion> allVersions = Enumerable.Empty<NuGetVersion>();
            var versionList = GetAllVersionsForPackageId(sourceRepository, identity.Id, project, includePrerelease);
            PackageIdentity safeUpdate = null;

            try
            {
                VersionRange spec = GetSafeRange(nugetVersion, includePrerelease);
                allVersions = versionList.Where(p => p < spec.MaxVersion && p >= spec.MinVersion);
                if (allVersions != null && allVersions.Any())
                {
                    NuGetVersion version = allVersions.OrderByDescending(v => v).FirstOrDefault();
                    safeUpdate = new PackageIdentity(identity.Id, version);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    Resources.Cmdlets_ErrorFindingUpdateVersion, identity.Id, ex.Message));
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
            IEnumerable<NuGetVersion> allVersions = GetAllVersionsForPackageId(sourceRepository, identity.Id, project, includePrerelease);
            PackageIdentity packageUpdate = null;
            NuGetVersion nVersion = null;

            try
            {
                // Find all versions that are higher than the package's current version
                allVersions = allVersions.Where(p => p > identity.Version).OrderByDescending(v => v);
                if (allVersions != null && allVersions.Any())
                {
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
                }

                if (nVersion != null)
                {
                    packageUpdate = new PackageIdentity(identity.Id, nVersion);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    Resources.Cmdlets_ErrorFindingUpdateVersion, identity.Id, ex.Message));
            }

            return packageUpdate;
        }
    }
}
