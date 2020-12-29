// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Struct containing installed and transitive packages for a project
    /// </summary>
    public struct ProjectPackages
    {
        public IReadOnlyList<PackageReference> InstalledPackages { get; }
        public IReadOnlyList<PackageReference> TransitivePackages { get; }

        public ProjectPackages(IReadOnlyList<PackageReference> installedPackages, IReadOnlyList<PackageReference> transitivePackages)
        {
            InstalledPackages = installedPackages ?? Array.Empty<PackageReference>();
            TransitivePackages = transitivePackages ?? Array.Empty<PackageReference>();
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(ProjectPackages left, ProjectPackages right)
        {
            throw new NotImplementedException();
        }

        public static bool operator !=(ProjectPackages left, ProjectPackages right)
        {
            throw new NotImplementedException();
        }
    }
}
