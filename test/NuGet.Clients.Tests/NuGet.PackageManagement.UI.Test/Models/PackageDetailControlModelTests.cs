// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;
using Moq;
using NuGet.ProjectManagement;
using NuGet.Test.Utility;

namespace NuGet.PackageManagement.UI.Test.Models
{
    public class PackageDetailControlModelTests : IClassFixture<LocalPackageSearchMetadataFixture>
    {
        private readonly LocalPackageSearchMetadataFixture _testData;
        private readonly PackageItemListViewModel _testViewModel;
        private readonly PackageDetailControlModel _testIntance;

        public PackageDetailControlModelTests(LocalPackageSearchMetadataFixture testData)
        {
            _testData = testData;
            _testViewModel = new PackageItemListViewModel()
            {
                LocalPackageInfo = _testData.TestData.LocalPackageInfo
            };

            var solMgr = new Mock<ISolutionManager>();
            var projectCollection = new List<NuGetProject>();
            _testIntance = new PackageDetailControlModel(solMgr.Object, projectCollection);
            var itemFiler = ItemFilter.All;
            _testIntance.SetCurrentPackage(
                _testViewModel,
                itemFiler,
                () => _testViewModel).Wait();
        }

        [Fact]
        public void PackageArchiveReader_NotNull()
        {
            Assert.NotNull(_testIntance.PackageArchiveReader);
        }
    }
}
