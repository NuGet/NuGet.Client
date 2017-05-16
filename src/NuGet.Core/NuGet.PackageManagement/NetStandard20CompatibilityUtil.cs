using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;

namespace NuGet.PackageManagement
{
    internal static class NetStandard20CompatibilityUtil
    {
        internal const string CompatibilityPackageId = "NETStandard.Library.NetFramework";

        /// <summary>
        /// This function is used to determine whether package "NETStandard.Library.NetFramework" 
        /// needs to be installed to the project to make NETStandard2.0 compatible
        /// with net461/net462/net47.
        /// </summary>
        internal static bool IsCompatibilityPackageNeededForProjectFramework(NuGetFramework currentProjectFramework)
        {
            if(currentProjectFramework == null)
            {
                throw new ArgumentNullException(nameof(currentProjectFramework));
            }

            if(currentProjectFramework.IsDesktop() 
                && currentProjectFramework.Version >= FrameworkConstants.CommonFrameworks.Net461.Version
                && currentProjectFramework.Version <= FrameworkConstants.CommonFrameworks.Net47.Version)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// If the install actions include the compatibility package itself, then we mark the boolean as false
        /// to avoid running into a recursive loop (since installing the compatibility package does need netstandard2.0 assets).
        /// </summary>
        /// <param name="actionsList"></param>
        internal static bool IsCompatibilityPackageBeingInstalled(IEnumerable<NuGetProjectAction> actionsList)
        {
            if (actionsList.Where(action => action.NuGetProjectActionType == NuGetProjectActionType.Install)
                            .Where(t => string.Equals(t.PackageIdentity.Id, CompatibilityPackageId, StringComparison.OrdinalIgnoreCase))
                            .Any())
            {
                return false;
            }

            return true;
        }
        internal static bool IsNearestFrameworkNetStandard20OrGreater(NuGetFramework currentProjectFramework,
            IEnumerable<NuGetFramework> supportedFrameworks)
        {
            // we look at target frameworks supported by the package and determine if the nearest framework is netstandard2.0,
            // then we do need to install the compatibility package. We only do it once for the whole list of actions -
            // hence the !needsNetstandard20Assets condition.
            var frameworkReducer = new FrameworkReducer();
            var nearestFramework = frameworkReducer.GetNearest(currentProjectFramework, supportedFrameworks);
            if (string.Equals(nearestFramework.Framework, FrameworkConstants.FrameworkIdentifiers.NetStandard, StringComparison.OrdinalIgnoreCase)
                    && nearestFramework.Version >= FrameworkConstants.CommonFrameworks.NetStandard20.Version)
            {
                return true;
            }

            return false;
        }

        internal static async Task InstallNetStandard20CompatibilityPackage(NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext,
            NuGetPackageManager packageManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            CancellationToken token)
        {
            // First check if the compatibility package is already installed.
            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);

            var installedPackageReference = projectInstalledPackageReferences
                .Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, CompatibilityPackageId))
                .FirstOrDefault();

            if (installedPackageReference != null)
            {
                return;
            }

            nuGetProjectContext.Log(
                    ProjectManagement.MessageLevel.Info,
                    Strings.InstallingNetstandard20CompatibilityPackage);

            var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest,
            includePrelease: false,
            includeUnlisted: false,
            versionConstraints: VersionConstraints.None);

            var primarySources = sourceRepositoryProvider.GetRepositories()
                .Where(e => e.PackageSource.IsEnabled);

            var log = new LoggerAdapter(nuGetProjectContext);

            // Check for latest stable version of the package.
            var resolvedPackage = await NuGetPackageManager.GetLatestVersionAsync(CompatibilityPackageId,
                nuGetProject,
                resolutionContext,
                primarySources,
                log,
                token);

            if (resolvedPackage?.LatestVersion == null)
            {
                // If no stable version of the package could be found, then look for latest pre-release.
                resolutionContext = new ResolutionContext(DependencyBehavior.Lowest,
                    includePrelease: true,
                    includeUnlisted: false,
                    versionConstraints: VersionConstraints.None);

                resolvedPackage = await NuGetPackageManager.GetLatestVersionAsync(CompatibilityPackageId,
                    nuGetProject,
                    resolutionContext,
                    primarySources,
                    log,
                    token);

                if (resolvedPackage == null || resolvedPackage.LatestVersion == null)
                {
                    throw new InvalidOperationException(string.Format(Strings.NoLatestVersionFound, CompatibilityPackageId));
                }

            }

            await packageManager.InstallPackageAsync(nuGetProject,
                new PackageIdentity(CompatibilityPackageId, resolvedPackage.LatestVersion),
                resolutionContext,
                nuGetProjectContext,
                primarySources,
                Enumerable.Empty<SourceRepository>(),
                token);
        }
    }
}
