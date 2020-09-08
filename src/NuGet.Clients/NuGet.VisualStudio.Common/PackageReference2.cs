// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Packaging;

namespace NuGet.Packaging
{
    public class PackageReference2 : PackageReference
    {
        //bool _isTransitive;

        public PackageReference2(PackageIdentity identity, NuGetFramework targetFramework, bool userInstalled, bool developmentDependency, bool requireReinstallation, VersionRange allowedVersions)
            : base(identity, targetFramework, userInstalled, developmentDependency, requireReinstallation, allowedVersions)
        {
        }
    }
}
