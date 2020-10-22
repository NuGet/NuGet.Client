// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageManagerListItemsTest
    {
        [Fact]
        public async Task PackagePrefixReservation_FromOneSource()
        {
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = new Mock<INuGetUIContext>();

            uiContext.Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);

            uiContext.Setup(x => x.ServiceBroker)
                .Returns(Mock.Of<IServiceBroker>());

            // Arrange
            var responses = new Dictionary<string, string>
            {
                {
                    "https://api-v3search-0.nuget.org/query?q=EntityFramework&skip=0&take=26&prerelease=false&semVerLevel=2.0.0",
                    ProtocolUtility.GetResource("NuGet.PackageManagement.UI.Test.compiler.resources.EntityFrameworkSearch.json", GetType())
                },
                { "http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);
            var repositories = new List<SourceRepository>
            {
                repo
            };

            var context = new PackageLoadContext(repositories, isSolution: false, uiContext.Object);

            var packageFeed = new MultiSourcePackageFeed(repositories, logger: null, telemetryService: null);
            var loader = new PackageItemLoader(context, packageFeed, "EntityFramework", includePrerelease: false);

            var loaded = new List<PackageItemListViewModel>();
            foreach (var page in Enumerable.Range(0, 5))
            {
                await loader.LoadNextAsync(progress: null, CancellationToken.None);
                while (loader.State.LoadingStatus == LoadingStatus.Loading)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await loader.UpdateStateAsync(progress: null, CancellationToken.None);
                }

                var items = loader.GetCurrent();
                loaded.AddRange(items);

                if (loader.State.LoadingStatus != LoadingStatus.Ready)
                {
                    break;
                }
            }

            // Resource only has one item
            var item = loaded.First();
            Assert.True(item.PrefixReserved);
        }

        [Fact]
        public async Task PackagePrefixReservation_FromMultiSource()
        {
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = new Mock<INuGetUIContext>();

            uiContext.Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);

            uiContext.Setup(x => x.ServiceBroker)
                .Returns(Mock.Of<IServiceBroker>());

            // Arrange
            var responses = new Dictionary<string, string>
            {
                {
                    "https://api-v3search-0.nuget.org/query?q=EntityFramework&skip=0&take=26&prerelease=false&semVerLevel=2.0.0",
                    ProtocolUtility.GetResource("NuGet.PackageManagement.UI.Test.compiler.resources.EntityFrameworkSearch.json", GetType())
                },
                { "http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer },
                { "http://othersource.com/v3/index.json", JsonData.IndexWithoutFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);
            var repo1 = StaticHttpHandler.CreateSource("http://othersource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var repositories = new List<SourceRepository>
            {
                repo,
                repo1
            };

            var context = new PackageLoadContext(repositories, isSolution: false, uiContext.Object);

            var packageFeed = new MultiSourcePackageFeed(repositories, logger: null, telemetryService: null);
            var loader = new PackageItemLoader(context, packageFeed, "EntityFramework", includePrerelease: false);

            var loaded = new List<PackageItemListViewModel>();
            foreach (var page in Enumerable.Range(0, 5))
            {
                await loader.LoadNextAsync(null, CancellationToken.None);
                while (loader.State.LoadingStatus == LoadingStatus.Loading)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await loader.UpdateStateAsync(progress: null, CancellationToken.None);
                }

                var items = loader.GetCurrent();
                loaded.AddRange(items);

                if (loader.State.LoadingStatus != LoadingStatus.Ready)
                {
                    break;
                }
            }

            // Resource only has one item
            var item = loaded.First();
            Assert.False(item.PrefixReserved);
        }
    }
}
