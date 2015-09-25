// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI
{
    public class PackageLicenseInfo
    {
        public PackageLicenseInfo(
            string id,
            Uri licenseUrl,
            string authors)
        {
            Id = id;
            LicenseUrl = licenseUrl;
            Authors = authors;
        }

        public string Id { get; private set; }

        public Uri LicenseUrl { get; private set; }

        public string Authors { get; private set; }
    }
}
