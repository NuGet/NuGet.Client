// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public struct NuGetProjectPackages
    {
        public IReadOnlyCollection<IPackageReferenceContextInfo> InstalledPackages { get; set; }
        public IReadOnlyCollection<IPackageReferenceContextInfo> TransitivePackages { get; set; }

        public NuGetProjectPackages(PackageReferenceContextInfo[] installedPackages, PackageReferenceContextInfo[] transitivePackages) : this()
        {
            InstalledPackages = installedPackages;
            TransitivePackages = transitivePackages;
        }

        public override bool Equals(object obj)
        {
            throw new System.NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new System.NotImplementedException();
        }

        public static bool operator ==(NuGetProjectPackages left, NuGetProjectPackages right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NuGetProjectPackages left, NuGetProjectPackages right)
        {
            return !(left == right);
        }
    }
}
