// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class BackgroundLoaderResult
    {
        public PackageStatus Status { get; set; }

        public NuGetVersion LatestVersion { get; set; }

        public NuGetVersion InstalledVersion { get; set; }
    }
}