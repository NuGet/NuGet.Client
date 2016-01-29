// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public interface IProjectDependencyProvider : IDependencyProvider
    {
        /// <summary>
        /// Resolves a local library.
        /// </summary>
        /// <param name="libraryRange">Dependency requirements.</param>
        /// <param name="targetFramework">Project framework.</param>
        /// <param name="rootPath">Root path of the parent project. This is used for resolving global.json.</param>
        /// <remarks>The root project is used if no <see cref="rootPath" /> is given.</remarks>
        Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework, string rootPath);
    }
}
