// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging;
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
                PackageReader = _testData.TestData.PackageReader,
            };
        }

        [Fact]
        public void LocalSources_PackageReader_NotNull()
        {
            Assert.NotNull(_testInstance.PackageReader);

            Func<PackageReaderBase> func = _testInstance.PackageReader;

            PackageReaderBase reader = func();
            Assert.IsType(typeof(PackageArchiveReader), reader);
        }
    }
}
