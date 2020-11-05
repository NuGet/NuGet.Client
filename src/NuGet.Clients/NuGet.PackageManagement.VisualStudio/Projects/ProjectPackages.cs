// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Struct containing installed and transitive packages for a project
    /// </summary>
    public struct ProjectPackages
    {
        public IReadOnlyList<PackageReference> InstalledPackages { get; set; }
        public IReadOnlyList<PackageReference> TransitivePackages { get; set; }

        public ProjectPackages(IReadOnlyList<PackageReference> installedPackages, IReadOnlyList<PackageReference> transitivePackages) : this()
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

        public static bool operator ==(ProjectPackages left, ProjectPackages right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProjectPackages left, ProjectPackages right)
        {
            return !(left == right);
        }
    }
}
