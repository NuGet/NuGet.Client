// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Resolver;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public interface INuGetProjectManagerService : IDisposable
    {
        ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(
            IReadOnlyCollection<string> projectIds,
            CancellationToken cancellationToken);
        ValueTask<IInstalledAndTransitivePackages> GetInstalledAndTransitivePackagesAsync(
            IReadOnlyCollection<string> projectIds,
            CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<NuGetFramework>> GetTargetFrameworksAsync(
            IReadOnlyCollection<string> projectIds,
            CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<PackageDependencyInfo>> GetInstalledPackagesDependencyInfoAsync(
            string projectId,
            bool includeUnresolved,
            CancellationToken cancellationToken);
        ValueTask<IProjectMetadataContextInfo> GetMetadataAsync(string projectId, CancellationToken cancellationToken);
        ValueTask<IProjectContextInfo> GetProjectAsync(string projectId, CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<IProjectContextInfo>> GetProjectsAsync(CancellationToken cancellationToken);
        ValueTask<(bool, string?)> TryGetInstalledPackageFilePathAsync(
            string projectId,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<IProjectContextInfo>> GetProjectsWithDeprecatedDotnetFrameworkAsync(
            CancellationToken cancellationToken);

        ValueTask BeginOperationAsync(CancellationToken cancellationToken);
        ValueTask EndOperationAsync(CancellationToken cancellationToken);
        ValueTask ExecuteActionsAsync(IReadOnlyList<ProjectAction> actions, CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<ProjectAction>> GetInstallActionsAsync(
            IReadOnlyCollection<string> projectIds,
            PackageIdentity packageIdentity,
            VersionConstraints versionConstraints,
            bool includePrelease,
            DependencyBehavior dependencyBehavior,
            IReadOnlyList<string> packageSourceNames,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<ProjectAction>> GetUninstallActionsAsync(
            IReadOnlyCollection<string> projectIds,
            PackageIdentity packageIdentity,
            bool removeDependencies,
            bool forceRemove,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<ProjectAction>> GetUpdateActionsAsync(
            IReadOnlyCollection<string> projectIds,
            IReadOnlyCollection<PackageIdentity> packageIdentities,
            VersionConstraints versionConstraints,
            bool includePrelease,
            DependencyBehavior dependencyBehavior,
            IReadOnlyList<string> packageSourceNames,
            CancellationToken cancellationToken);
    }
}
