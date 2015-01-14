using NuGet.Client;
using NuGet.Client.V3.VisualStudio;
using NuGet.Client.VisualStudio;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NuGet.PackagingCore;
using NuGet.Frameworks;
using NuGet.ProjectManagement;

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

        public static PackageIdentity GetLatestPackageIdentityForId(string packageId, NuGetProject project, bool includePrerelease, SourceRepository sourceRepository)
        {
            string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
            List<string> targetFrameworks = new List<string>() { framework };
            SearchFilter searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;

            MetadataResource resource = sourceRepository.GetResource<MetadataResource>();
            Task<NuGetVersion> task = resource.GetLatestVersion(packageId, includePrerelease, false, CancellationToken.None);
            NuGetVersion latestVersion = task.Result;
            return new PackageIdentity(packageId, latestVersion);
        }
    }
}
