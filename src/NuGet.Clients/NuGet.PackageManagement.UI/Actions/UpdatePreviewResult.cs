// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class UpdatePreviewResult
    {
        public PackageIdentity Old { get; }
        public PackageIdentity New { get; }
        public string AutomationName { get; }
        public VersionRange OldVersionRange { get; }
        public VersionRange NewVersionRange { get; }

        public UpdatePreviewResult(PackageIdentity oldPackage, PackageIdentity newPackage)
            : this(oldPackage, newPackage, oldPackageVersionRange: null, newPackageVersionRange: null)
        {
        }

        public UpdatePreviewResult(PackageIdentity oldPackage, PackageIdentity newPackage, VersionRange oldPackageVersionRange, VersionRange newPackageVersionRange)
        {
            Old = oldPackage;
            New = newPackage;
            OldVersionRange = oldPackageVersionRange ?? VersionRange.Parse(oldPackage.Version.ToString());
            NewVersionRange = newPackageVersionRange ?? VersionRange.Parse(newPackage.Version.ToString()); ;
            AutomationName = string.Format(
                CultureInfo.CurrentUICulture,
                Resources.Preview_PackageUpdate,
                Old.Id, Old.Version.ToNormalizedString(),
                New.Id, New.Version.ToNormalizedString());
        }

        public override string ToString()
        {
            return Old + " -> " + New;
        }
    }
}
