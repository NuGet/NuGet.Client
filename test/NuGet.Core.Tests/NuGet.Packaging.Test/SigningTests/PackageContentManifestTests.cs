// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test.SigningTests
{
    public class PackageContentManifestTests
    {
        [Fact]
        public void PackageContentManifest_CreateManifestWithNoFiles()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>();

            var manifest = PackageContentManifest.Create(version, HashAlgorithm.SHA256, files);

        }
    }
}
