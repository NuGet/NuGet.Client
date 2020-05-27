// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio.RemoteTypes;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface INuGetSourceRepositoryService : IDisposable
    {
        event EventHandler PackageSourcesChanged;
        Task<IReadOnlyList<RemoteSourceRepository>> GetRepositoriesAsync(CancellationToken cancellationToken);
        Task<SourceRepository> CreateRepositoryAsync(PackageSource source, CancellationToken cancellationToken);
        Task<SourceRepository> CreateRepositoryAsync(PackageSource source, FeedType type, CancellationToken cancellationToken);
        Task<IReadOnlyList<PackageSource>> GetAllPackageSourcesAsync(CancellationToken cancellationToken);
        Task SaveAllPackageSourcesAsync(IReadOnlyList<PackageSource> packageSources, CancellationToken cancellationToken);
        Task<IPackageSourceProvider> GetPackageSourceProviderAsync(CancellationToken cancellationToken);
        Task AddPackageSourceAsync(PackageSource source, CancellationToken cancellationToken);
        Task DisablePackageSourceAsync(string name, CancellationToken cancellationToken);
        Task EnablePackageSourceAsync(string name, CancellationToken cancellationToken);
        Task<PackageSource> GetPackageSourceByNameAsync(string name, CancellationToken cancellationToken);
        Task<PackageSource> GetPackageSourceBySourceAsync(string source, CancellationToken cancellationToken);
        Task<bool> IsPackageSourceEnabledAsync(string name, CancellationToken cancellationToken);
        Task<IReadOnlyList<PackageSource>> LoadPackageSourcesAsync(CancellationToken cancellationToken);
        Task RemovePackageSourceAsync(string name, CancellationToken cancellationToken);
        Task SaveActivePackageSourceAsync(PackageSource source, CancellationToken cancellationToken);
        Task SavePackageSourcesAsync(IEnumerable<PackageSource> sources, CancellationToken cancellationToken);
        Task UpdatePackageSourceAsync(PackageSource source, bool updateCredentials, bool updateEnabled, CancellationToken cancellationToken);
        Task<string> GetActivePackageSourceNameAsync(CancellationToken cancellationToken);
        Task<string> GetDefaultPushSourceAsync(CancellationToken cancellationToken);
    }
}
