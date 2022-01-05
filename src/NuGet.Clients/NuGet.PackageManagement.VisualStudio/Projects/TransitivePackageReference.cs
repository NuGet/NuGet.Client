// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.VisualStudio
{
    public class TransitivePackageReference : PackageReference
    {
        public TransitivePackageReference(PackageIdentity identity, NuGetFramework targetFramework)
            : base(identity, targetFramework, true)
        {
        }

        public TransitivePackageReference(PackageReference pr)
            : base(pr?.PackageIdentity ?? throw new ArgumentNullException(nameof(pr)), pr.TargetFramework, pr.IsUserInstalled, pr.IsDevelopmentDependency, pr.RequireReinstallation, pr.AllowedVersions)
        {


        }


        public IEnumerable<PackageReference> TransitiveOrigins { get; set; }
    }
}
