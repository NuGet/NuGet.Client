// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    public class PackageReferenceComparer : IEqualityComparer<Packaging.PackageReference>
    {
        private readonly PackageIdentityComparer _packageIdentityComparer = new PackageIdentityComparer();

        public bool Equals(Packaging.PackageReference x, Packaging.PackageReference y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return _packageIdentityComparer.Equals(x.PackageIdentity, y.PackageIdentity);
        }

        public int GetHashCode(Packaging.PackageReference obj)
        {
            return _packageIdentityComparer.GetHashCode(obj.PackageIdentity);
        }
    }
}
