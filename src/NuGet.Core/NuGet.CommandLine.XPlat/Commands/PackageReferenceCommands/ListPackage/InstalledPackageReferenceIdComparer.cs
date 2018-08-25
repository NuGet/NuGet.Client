// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    class InstalledPackageReferenceIdComparer : IEqualityComparer<InstalledPackageReference>
    {
        public bool Equals(InstalledPackageReference one, InstalledPackageReference two)
        {
            return one.Name.Equals(two.Name);
        }

        public int GetHashCode(InstalledPackageReference item)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(item.Name);
        }
    }
}
