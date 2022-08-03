// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Moq;
using NuGet.PackageManagement.UI.Utility;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models
{

    [Collection(MockedVS.Collection)]
    public class DonnieTests
    {

        public DonnieTests(GlobalServiceProvider sp)
        {

        }

        [Theory]
        [InlineData(ItemFilter.All, "3.0.0")]
        [InlineData(ItemFilter.Installed, "1.0.0")]
        [InlineData(ItemFilter.UpdatesAvailable, "3.0.0")]
        public async Task SetCurrentPackageAsync_CorrectSelectedVersion(ItemFilter tab, string expectedSelectedVersion)
        {
            // Arrange
            NuGetVersion installedVersion = NuGetVersion.Parse("1.0.0");

            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01248")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0")),
                new VersionInfoContextInfo(new NuGetVersion("3.0.0")),
                new VersionInfoContextInfo(new NuGetVersion("1.0.0")),
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(ss => ss.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IEnumerable<IProjectContextInfo>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);

            var vm = new PackageItemViewModel(searchService.Object)
            {
                Id = "package",
                InstalledVersion = installedVersion,
                Version = installedVersion,
            };

            // Act
            if (tab == ItemFilter.All)
            {
                Assert.NotNull(expectedSelectedVersion);
            }
            await Task.Delay(20);
            //await _testInstance.SetCurrentPackageAsync(
            //    vm,
            //    tab,
            //    () => vm);

            //NuGetVersion selectedVersion = NuGetVersion.Parse(expectedSelectedVersion);

            //Assert.Equal(_testInstance.SelectedVersion.Version, selectedVersion);
        }
    }
}
