// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio.Internal.Contracts
{
    /// <summary>
    /// Contains information about a NuGetProject
    /// </summary>
    public interface IProjectContextInfo
    {
        string UniqueId { get; }
        ProjectStyle ProjectStyle { get; }
        NuGetProjectKind ProjectKind { get; }
        ValueTask<bool> IsUpgradeableAsync(CancellationToken cancellationToken);
        Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken cancellationToken);
        ValueTask<(bool, T)> TryGetMetadataAsync<T>(string key, CancellationToken cancellationToken);
        ValueTask<T> GetMetadataAsync<T>(string key, CancellationToken cancellationToken);
        ValueTask<string> GetUniqueNameOrNameAsync(CancellationToken cancellationToken);
    }
}
