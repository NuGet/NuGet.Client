// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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
        public static NuGetVersion GetNuGetVersionFromString(string version)
        {
            NuGetVersion nVersion;
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }
            var success = NuGetVersion.TryParse(version, out nVersion);
            if (!success)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                        Resources.Cmdlet_FailToParseVersion, version));
            }
            return nVersion;
        }

        /// <summary>
        /// Get project's target frameworks
        /// </summary>
        public static IEnumerable<string> GetProjectTargetFrameworks(NuGetProject project)
        {
            var frameworks = new List<string>();
            var nugetFramework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
            if (nugetFramework != null)
            {
                var framework = nugetFramework.ToString();
                frameworks.Add(framework);
            }
            return frameworks;
        }

        /// <summary>
        /// Get all versions for a specific package Id.
        /// </summary>
        public static IEnumerable<NuGetVersion> GetAllVersionsForPackageId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease)
        {
            var targetFrameworks = GetProjectTargetFrameworks(project);
            var searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;
            var resource = sourceRepository.GetResource<PSSearchResource>();
            PSSearchMetadata result = null;
            var allVersions = Enumerable.Empty<NuGetVersion>();

            try
            {
                var task = resource.Search(packageId, searchfilter, 0, 30, CancellationToken.None);
                result = task.Result
                    .Where(p => string.Equals(p.Identity.Id, packageId, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                allVersions = result.Versions.Value.Result;
            }
            catch (Exception)
            {
                if (result == null
                    || !allVersions.Any())
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture,
                            Resources.UnknownPackage, packageId));
                }
            }

            return allVersions;
        }

        /// <summary>
        /// Return the latest version for package Id.
        /// </summary>
        public static NuGetVersion GetLastestVersionForPackageId(SourceRepository sourceRepository, string packageId, NuGetProject project, bool includePrerelease)
        {
            var versionList = GetAllVersionsForPackageId(sourceRepository, packageId, project, includePrerelease);
            return versionList.OrderByDescending(v => v).FirstOrDefault();
        }

        /// <summary>
        /// Get safe update version for installed package identity. Used for Update-Package -Safe.
        /// </summary>
        public static PackageIdentity GetSafeUpdateForPackageIdentity(SourceRepository sourceRepository, PackageIdentity identity, NuGetProject project, bool includePrerelease, NuGetVersion nugetVersion)
        {
            var allVersions = Enumerable.Empty<NuGetVersion>();
            var versionList = GetAllVersionsForPackageId(sourceRepository, identity.Id, project, includePrerelease);
            PackageIdentity safeUpdate = null;

            try
            {
                var spec = GetSafeRange(nugetVersion, includePrerelease);
                allVersions = versionList.Where(p => p < spec.MaxVersion && p >= spec.MinVersion);
                if (allVersions != null
                    && allVersions.Any())
                {
                    var version = allVersions.OrderByDescending(v => v).FirstOrDefault();
                    safeUpdate = new PackageIdentity(identity.Id, version);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                        Resources.Cmdlets_ErrorFindingUpdateVersion, identity.Id, ex.Message));
            }

            return safeUpdate;
        }

        /// <summary>
        /// The safe range is defined as the highest build and revision for a given major and minor version
        /// </summary>
        public static VersionRange GetSafeRange(NuGetVersion version, bool includePrerelease)
        {
            var max = new Versioning.SemanticVersion(version.Major, version.Minor + 1, 0);
            var maxVersion = NuGetVersion.Parse(max.ToString());
            return new VersionRange(version, true, maxVersion, false, includePrerelease);
        }

        /// <summary>
        /// Get the update version for Dependent package, based on the specification of Highest, HighestMinor,
        /// HighestPatch and Lowest.
        /// </summary>
        public static PackageIdentity GetUpdateForPackageByDependencyEnum(SourceRepository sourceRepository, PackageIdentity identity, NuGetProject project, DependencyBehavior updateVersion, bool includePrerelease)
        {
            var allVersions = GetAllVersionsForPackageId(sourceRepository, identity.Id, project, includePrerelease)
                ?? Enumerable.Empty<NuGetVersion>();

            if (!allVersions.Any())
            {
                return null;
            }

            allVersions = allVersions.Where(p => p > identity.Version).OrderByDescending(v => v);

            NuGetVersion version = null;

            try
            {
                // Find all versions that are higher than the package's current version
                if (updateVersion == DependencyBehavior.Lowest)
                {
                    version = allVersions.LastOrDefault();
                }
                else if (updateVersion == DependencyBehavior.Highest)
                {
                    version = allVersions.FirstOrDefault();
                }
                else if (updateVersion == DependencyBehavior.HighestPatch)
                {
                    var groups = from p in allVersions
                                 group p by new { p.Version.Major, p.Version.Minor }
                        into g
                                 orderby g.Key.Major, g.Key.Minor
                                 select g;
                    version = (from p in groups.First()
                               orderby p.Version descending
                               select p).FirstOrDefault();
                }
                else if (updateVersion == DependencyBehavior.HighestMinor)
                {
                    var groups = from p in allVersions
                                 group p by new { p.Version.Major }
                        into g
                                 orderby g.Key.Major
                                 select g;
                    version = (from p in groups.First()
                               orderby p.Version descending
                               select p).FirstOrDefault();
                }

                if (version != null)
                {
                    return new PackageIdentity(identity.Id, version);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                        Resources.Cmdlets_ErrorFindingUpdateVersion, identity.Id, ex.Message));
            }

            return null;
        }
    }
}
