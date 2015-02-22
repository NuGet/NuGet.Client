// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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

        public Task<RemoteMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var description = _dependencyProvider.GetDescription(libraryRange, targetFramework);

            if (description == null)
            {
                return Task.FromResult<RemoteMatch>(null);
            }

            return Task.FromResult(new RemoteMatch
            {
                Library = description.Identity,
                Path = description.Path,
                Provider = this,
            });
        }

        public Task<IEnumerable<LibraryDependency>> GetDependencies(RemoteMatch match, NuGetFramework targetFramework)
        {
            var description = _dependencyProvider.GetDescription(match.Library, targetFramework);

            return Task.FromResult(description.Dependencies);
        }

        public Task CopyToAsync(RemoteMatch match, Stream stream)
        {
            // We never call this on local providers
            throw new NotImplementedException();
        }
    }
}
