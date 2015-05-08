// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;

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

        public Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            var library = _dependencyProvider.GetLibrary(libraryRange, targetFramework);

            if (library == null)
            {
                return Task.FromResult<LibraryIdentity>(null);
            }

            return Task.FromResult(library.Identity);
        }

        public Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity library, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            var description = _dependencyProvider.GetLibrary(library, targetFramework);

            return Task.FromResult(description.Dependencies);
        }

        public Task CopyToAsync(LibraryIdentity match, Stream stream, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
