// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.UI
{
    public class ShowUpdatePackageOptions
    {
        public ShowUpdatePackageOptions(bool shouldUpdateAllPackages = false, IEnumerable<string> packagesToUpdate = null)
        {
            ShouldUpdateAllPackages = shouldUpdateAllPackages;
            PackagesToUpdate = packagesToUpdate ?? Enumerable.Empty<string>();
        }

        public bool ShouldUpdateAllPackages { get; }

        public IEnumerable<string> PackagesToUpdate { get; }
    }
}
