// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Struct containing installed and transitive package collections for a project
    /// </summary>
    public struct ProjectPackageCollections
    {
        public PackageCollection InstalledPackages { get; set; }
        public PackageCollection TransitivePackages { get; set; }

        public ProjectPackageCollections(PackageCollection installedPackages, PackageCollection transitivePackages) : this()
        {
            InstalledPackages = installedPackages;
            TransitivePackages = transitivePackages;
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(ProjectPackageCollections left, ProjectPackageCollections right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProjectPackageCollections left, ProjectPackageCollections right)
        {
            return !(left == right);
        }
    }
}
