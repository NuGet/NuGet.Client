// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Protocol.Core.Types.Tests
{
    public class PackageSearchMetadataBuilderTests : IClassFixture<LocalPackageSearchMetadataFixture>
    {
        private readonly LocalPackageSearchMetadataFixture _testData;

        public PackageSearchMetadataBuilderTests(LocalPackageSearchMetadataFixture testData)
        {
            _testData = testData;
        }

        [Fact]        
        public void LocalPacakgeSearchMetadata_LocalPackageInfo_NotNull()
        {
            var copy1 = PackageSearchMetadataBuilder
                .FromMetadata(_testData.TestData)
                .Build();
            Assert.True(copy1 is PackageSearchMetadataBuilder.ClonedPackageSearchMetadata);

            var clone1 = copy1 as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata;
            Assert.NotNull(clone1.LocalPackageInfo);

            var copy2 = PackageSearchMetadataBuilder
                .FromMetadata(copy1)
                .Build();
            Assert.True(copy2 is PackageSearchMetadataBuilder.ClonedPackageSearchMetadata);
                
            var clone2 = copy2 as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata;
            Assert.NotNull(clone2.LocalPackageInfo);
        }
    }
}
