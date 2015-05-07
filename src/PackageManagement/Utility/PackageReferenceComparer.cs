// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    public class PackageReferenceComparer : IEqualityComparer<PackageReference>
    {
        private readonly PackageIdentityComparer _packageIdentityComparer = new PackageIdentityComparer();

        public bool Equals(PackageReference x, PackageReference y)
        {
            return _packageIdentityComparer.Equals(x.PackageIdentity, y.PackageIdentity);
        }

        public int GetHashCode(PackageReference obj)
        {
            return _packageIdentityComparer.GetHashCode(obj.PackageIdentity);
        }
    }
}
