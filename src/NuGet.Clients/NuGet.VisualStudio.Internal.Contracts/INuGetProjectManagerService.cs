// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public interface INuGetProjectManagerService : IDisposable
    {
        ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(
            IReadOnlyCollection<string> projectIds,
            CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<PackageDependencyInfo>> GetInstalledPackagesDependencyInfoAsync(
            string projectId,
            bool includeUnresolved,
            CancellationToken cancellationToken);
        ValueTask<object> GetMetadataAsync(string projectId, string key, CancellationToken cancellationToken);
        ValueTask<(bool, object)> TryGetMetadataAsync(string projectId, string key, CancellationToken cancellationToken);
        ValueTask<IProjectContextInfo> GetProjectAsync(string projectId, CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<IProjectContextInfo>> GetProjectsAsync(CancellationToken cancellationToken);
        ValueTask<(bool, string?)> TryGetInstalledPackageFilePathAsync(
            string projectId,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken);
    }
}
