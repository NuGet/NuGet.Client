// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    public interface IPackageSpecResolver
    {
        IEnumerable<string> SearchPaths { get; }

        bool TryResolvePackageSpec(string name, out PackageSpec packageSpec);
    }
}
