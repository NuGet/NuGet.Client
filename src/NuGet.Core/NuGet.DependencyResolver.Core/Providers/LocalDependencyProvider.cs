// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.DependencyResolver
{
    public class LocalDependencyProvider : IRemoteDependencyProvider
    {
        private readonly IDependencyProvider _dependencyProvider;

        public LocalDependencyProvider(IDependencyProvider dependencyProvider)
        {
            _dependencyProvider = dependencyProvider;
        }

        public bool IsHttp { get; private set; }

        public PackageSource Source { get; private set; }

        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var library = _dependencyProvider.GetLibrary(libraryRange, targetFramework);

            if (library == null)
            {
                return Task.FromResult<LibraryIdentity>(null);
            }

            return Task.FromResult(library.Identity);
        }

        public Task<LibraryDependencyInfo> GetDependenciesAsync(
            LibraryIdentity library,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var description = _dependencyProvider.GetLibrary(library, targetFramework);

            var dependencyInfo = LibraryDependencyInfo.Create(
                description.Identity,
                targetFramework,
                description.Dependencies);

            return Task.FromResult(dependencyInfo);
        }

        public Task CopyToAsync(
            LibraryIdentity match,
            Stream stream,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
