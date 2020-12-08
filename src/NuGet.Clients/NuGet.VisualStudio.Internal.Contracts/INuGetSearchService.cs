// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public interface INuGetSearchService : IDisposable
    {
        ValueTask<SearchResultContextInfo> RefreshSearchAsync(CancellationToken cancellationToken);

        ValueTask<SearchResultContextInfo> ContinueSearchAsync(CancellationToken cancellationToken);

        ValueTask<SearchResultContextInfo> SearchAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            string searchText,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            bool useRecommender,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetAllPackagesAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken);

        ValueTask<int> GetTotalCountAsync(
            int maxCount,
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> GetPackageVersionsAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken);

        ValueTask<PackageDeprecationMetadataContextInfo?> GetDeprecationMetadataAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetPackageMetadataListAsync(
            string id,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            bool includeUnlisted,
            CancellationToken cancellationToken);
    }
}
