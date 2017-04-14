// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.DependencyResolver
{
    public interface IRemoteDependencyProvider
    {
        bool IsHttp { get; }

        /// <summary>
        /// Feed package source.
        /// </summary>
        /// <remarks>Optional. This will be null for project providers.</remarks>
        PackageSource Source { get; }

        Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken);

        Task<LibraryDependencyInfo> GetDependenciesAsync(
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
