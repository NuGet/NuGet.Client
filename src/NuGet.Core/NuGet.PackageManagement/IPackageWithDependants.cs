// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    public interface IPackageWithDependants
    {
        PackageIdentity Identity { get; }

        IList<PackageIdentity> DependingPackages { get; }
    }
}
