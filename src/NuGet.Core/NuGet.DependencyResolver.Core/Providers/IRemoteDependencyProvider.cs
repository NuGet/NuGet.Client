// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.DependencyResolver
{
    public interface IRemoteDependencyProvider
    {
        bool IsHttp { get; }

        Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken);

        Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(
            LibraryIdentity match,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken);

        Task CopyToAsync(
            LibraryIdentity match,
            Stream stream,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken);
    }
}
