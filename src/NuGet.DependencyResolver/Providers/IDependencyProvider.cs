// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public interface IDependencyProvider
    {
        bool SupportsType(string libraryType);

        LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework);
    }
}
