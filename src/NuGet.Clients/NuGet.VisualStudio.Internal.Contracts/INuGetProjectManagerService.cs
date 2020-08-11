// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public interface INuGetProjectManagerService : IDisposable
    {
        ValueTask<IReadOnlyCollection<PackageReference>> GetInstalledPackagesAsync(IReadOnlyCollection<string> projectGuids, CancellationToken cancellationToken);
        ValueTask<object> GetMetadataAsync(string projectGuid, string key, CancellationToken cancellationToken);
        ValueTask<(bool, object)> TryGetMetadataAsync(string projectGuid, string key, CancellationToken cancellationToken);
        ValueTask<IProjectContextInfo> GetProjectAsync(string projectGuid, CancellationToken cancellationToken);
        ValueTask<IReadOnlyCollection<IProjectContextInfo>> GetProjectsAsync(CancellationToken cancellationToken);
        ValueTask<bool> IsProjectUpgradeableAsync(string projectGuid, CancellationToken cancellationToken);
    }
}
