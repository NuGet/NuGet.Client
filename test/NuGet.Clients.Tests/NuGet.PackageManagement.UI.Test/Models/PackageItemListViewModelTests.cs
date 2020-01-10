// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageItemListViewModelTests : IClassFixture<LocalPackageSearchMetadataFixture>
    {
        private readonly LocalPackageSearchMetadataFixture _testData;
        private readonly PackageItemListViewModel _testInstance;

        public PackageItemListViewModelTests(LocalPackageSearchMetadataFixture testData)
        {
            _testData = testData;
            _testInstance = new PackageItemListViewModel()
            {
                LocalPackageInfo = _testData.TestData.LocalPackageInfo
            };
        }

        [Fact]
        public void LocalSources_LocalPackageInfo_NotNull()
        {
            Assert.NotNull(_testInstance.LocalPackageInfo);
        }

        [Fact]
        public void LocalSources_PackageArchiveReader_NotNull()
        {
            Assert.NotNull(_testInstance.PackageArchiveReader);
        }
    }
}
