// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class LocalPackageSearchMetadataTests
    {
        [Fact]
        public async Task DeprecationMetadataIsNull()
        {
            var localPackageInfo = new LocalPackageInfo(
                new PackageIdentity("id", NuGetVersion.Parse("1.0.0")),
                "path",
                new DateTime(2019, 8, 19),
                new Lazy<NuspecReader>(() => null),
                () => null);

            var localPackageSearchMetadata = new LocalPackageSearchMetadata(localPackageInfo);

            Assert.Null(await localPackageSearchMetadata.GetDeprecationMetadataAsync());
        }
    }
}
