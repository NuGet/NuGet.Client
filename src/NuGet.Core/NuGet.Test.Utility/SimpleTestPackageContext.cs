// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public class SimpleTestPackageContext
    {
        public string Id { get; set; } = "packageA";
        public string Version { get; set; } = "1.0.0";
        public List<SimpleTestPackageContext> Dependencies { get; set; } = new List<SimpleTestPackageContext>();
        public string Include { get; set; } = string.Empty;
        public string Exclude { get; set; } = string.Empty;

        public PackageIdentity Identity
        {
            get
            {
                return new PackageIdentity(Id, NuGetVersion.Parse(Version));
            }
        }
    }
}
