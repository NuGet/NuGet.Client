// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Test.Utility;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(MockedVS.Collection)]
    public class BaseMockedVSCollectionTest : IAsyncServiceProvider
    {
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>();

        public BaseMockedVSCollectionTest()
        {
            ServiceLocator.InitializePackageServiceProvider(this);
        }

        protected void AddService<T>(Task<object> obj)
        {
            _services.Add(typeof(T), obj);
        }

        public Task<object> GetServiceAsync(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out Task<object> task))
            {
                return task;
            }

            return Task.FromResult<object>(null);
        }
    }

    public class NuGetPackageSearchServiceTests : BaseMockedVSCollectionTest
    {
        SourceRepository _sourceRepository;
        IEnumerable<IPackageReferenceContextInfo> _installedPackages;
        IReadOnlyCollection<IProjectContextInfo> _projects;

        public NuGetPackageSearchServiceTests()
        {
            _installedPackages = new List<IPackageReferenceContextInfo>();
            _projects = new List<IProjectContextInfo> { new ProjectContextInfo(Guid.NewGuid().ToString(), ProjectModel.ProjectStyle.PackageReference, NuGetProjectKind.PackageReference) };

            var query = "https://api-v2v3search-0.nuget.org/query";
            var responses = new Dictionary<string, string>
            {

                { NuGetConstants.V3FeedUrl, ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.index.json", GetType()) },
                { query + "?q=nuget&skip=0&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetSearchPage1.json", GetType()) },
                { query + "?q=nuget&skip=25&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetSearchPage2.json", GetType()) },
                { query + "?q=&skip=0&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.blankSearchPage.json", GetType()) },
                { "https://api.nuget.org/v3/registration3-gz-semver2/" + "nuget.core/index.json", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetCoreIndex.json", GetType()) }
            };

            _sourceRepository = StaticHttpHandler.CreateSource(NuGetConstants.V3FeedUrl, Repository.Provider.GetCoreV3(), responses);
        }

        [Fact]
        public async Task GetTotalCountAsync_MaxCount()
        {
            var source1 = new PackageSource("https://dotnet.myget.org/F/nuget-volatile/api/v3/index.json", "NuGetVolatile");
            var source2 = new PackageSource("https://api.nuget.org/v3/index.json", "NuGet.org");
            var sources = new List<PackageSource> { source1, source2 };
            var sourceRepository = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { source1, source2 });

            AddService<ISourceRepositoryProvider>(Task.FromResult<object>(sourceRepository));

            var serviceActivationOptions = default(ServiceActivationOptions);
            var serviceBroker = new Mock<IServiceBroker>();
            var authorizationService = new AuthorizationServiceClient(Mock.Of<IAuthorizationService>());

            var sharedState = new SharedServiceState();

            var projectManagerService = new Mock<INuGetProjectManagerService>();

            var installedPackages = new List<IPackageReferenceContextInfo>();
            var projects = new List<IProjectContextInfo> { new ProjectContextInfo(Guid.NewGuid().ToString(), ProjectModel.ProjectStyle.PackageReference, NuGetProjectKind.PackageReference) };

            projectManagerService.Setup(x =>
                x.GetInstalledPackagesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>>(installedPackages));

#pragma warning disable ISB001 // Dispose of proxies
            serviceBroker.Setup(x =>
                x.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<INuGetProjectManagerService>(projectManagerService.Object));
#pragma warning restore ISB001 // Dispose of proxies

            NuGetPackageSearchService searchService = new NuGetPackageSearchService(serviceActivationOptions, serviceBroker.Object, authorizationService, sharedState);

            var totalCount = await searchService.GetTotalCountAsync(100, projects, sources, new SearchFilter(true), ItemFilter.All, CancellationToken.None);

            Assert.Equal(100, totalCount);
        }

        [Fact]
        public async Task GetTotalCountAsync_Works()
        {
            var searchService = SetupSearchService();
            var totalCount = await searchService.GetTotalCountAsync(100, _projects, new List<PackageSource> { _sourceRepository.PackageSource }, new SearchFilter(true), ItemFilter.All, CancellationToken.None);

            Assert.Equal(22, totalCount);
        }

        [Fact]
        public async Task GetAllPackagesAsync_Works()
        {
            var searchService = SetupSearchService();
            var allPackages = await searchService.GetAllPackagesAsync(_projects, new List<PackageSource> { _sourceRepository.PackageSource }, new SearchFilter(true), ItemFilter.All, CancellationToken.None);

            Assert.Equal(22, allPackages.Count);
        }

        [Fact]
        public async Task GetPackageMetadataListAsync_Works()
        {
            var searchService = SetupSearchService();
            var packageMetadataList = await searchService.GetPackageMetadataListAsync("NuGet.Core", new List<PackageSource> { _sourceRepository.PackageSource }, true, true, CancellationToken.None);

            Assert.Equal(57, packageMetadataList.Count);
        }

        [Fact]
        public async Task GetDeprecationMetadataAsync_Works()
        {
            var searchService = SetupSearchService();
            var deprecationMetadata = await searchService.GetDeprecationMetadataAsync(new PackageIdentity("NuGet.Core", new Versioning.NuGetVersion("2.14.0")), new List<PackageSource> { _sourceRepository.PackageSource }, true, CancellationToken.None);

            Assert.NotNull(deprecationMetadata);
            Assert.Equal("NuGet.Core is part of NuGet client v2 APIs. They have been replaced by NuGet client v3 and later APIs.", deprecationMetadata.Message);
            Assert.Equal("Legacy", deprecationMetadata.Reasons.First());
        }

        [Fact]
        public async Task GetPackageVersionsAsync_Works()
        {
            NuGetPackageSearchService searchService = SetupSearchService();
            var result = await searchService.GetPackageVersionsAsync(new PackageIdentity("NuGet.Core", new Versioning.NuGetVersion("2.14.0")), new List<PackageSource> { _sourceRepository.PackageSource }, true, CancellationToken.None);
            Assert.Equal(51, result.Count);
            Assert.True(result.Last().Version.Version.Equals(new Version("1.0.1120.104")));
        }

        [Fact]
        public async Task SearchAndContinueSearch_Works()
        {
            var telemetryService = new Mock<INuGetTelemetryService>();
            var eventsQueue = new ConcurrentQueue<TelemetryEvent>();
            telemetryService
                .Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(e => eventsQueue.Enqueue(e));

            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            var searchService = SetupSearchService();
            var searchResult = await searchService.SearchAsync(_projects, new List<PackageSource> { _sourceRepository.PackageSource }, "nuget", new SearchFilter(true), ItemFilter.All, true, CancellationToken.None);
            var continueSearchResult = await searchService.ContinueSearchAsync(CancellationToken.None);

            Assert.True(searchResult.PackageSearchItems.First().Title.Equals("NuGet.Core1", StringComparison.OrdinalIgnoreCase));
            Assert.True(continueSearchResult.PackageSearchItems.First().Title.Equals("NuGet.Core27", StringComparison.OrdinalIgnoreCase));

            var events = eventsQueue.ToArray();
            Assert.True(4 == events.Length, string.Join(Environment.NewLine, events.Select(e => e.Name)));

            var search = Assert.Single(events, e => e.Name == "Search");
            Assert.Equal(true, search["IncludePrerelease"]);
            Assert.Equal("nuget", search.GetPiiData().First(p => p.Key == "Query").Value);
            var operationId = Assert.IsType<string>(search["OperationId"]);
            var parsedOperationId = Guid.ParseExact(operationId, "D");

            var sources = Assert.Single(events, e => e.Name == "SearchPackageSourceSummary");
            Assert.Equal(1, sources["NumHTTPv3Feeds"]);
            Assert.Equal("YesV3", sources["NuGetOrg"]);
            Assert.Equal(operationId, sources["ParentId"]);

            var page0 = Assert.Single(events, e => e.Name == "SearchPage" && e["PageIndex"] is int && (int)e["PageIndex"] == 0);
            Assert.Equal("Ready", page0["LoadingStatus"]);
            Assert.Equal(operationId, page0["ParentId"]);
            Assert.IsType<int>(page0["ResultCount"]);
            Assert.IsType<double>(page0["Duration"]);
            Assert.IsType<double>(page0["ResultsAggregationDuration"]);
            Assert.IsType<string>(page0["IndividualSourceDurations"]);
            Assert.Equal(1, ((JArray)JsonConvert.DeserializeObject((string)page0["IndividualSourceDurations"])).Values<double>().Count());

            var page1 = Assert.Single(events, e => e.Name == "SearchPage" && e["PageIndex"] is int && (int)e["PageIndex"] == 1);
            Assert.Equal("Ready", page1["LoadingStatus"]);
            Assert.Equal(operationId, page1["ParentId"]);
            Assert.IsType<int>(page1["ResultCount"]);
            Assert.IsType<double>(page1["Duration"]);
            Assert.IsType<double>(page1["ResultsAggregationDuration"]);
            Assert.IsType<string>(page1["IndividualSourceDurations"]);
            Assert.Equal(1, ((JArray)JsonConvert.DeserializeObject((string)page1["IndividualSourceDurations"])).Values<double>().Count());
        }

        private NuGetPackageSearchService SetupSearchService()
        {
            var sourceRepositoryProvider = new Mock<ISourceRepositoryProvider>();
            sourceRepositoryProvider.Setup(x => x.CreateRepository(It.IsAny<PackageSource>())).Returns(_sourceRepository);
            var solutionManager = new Mock<IVsSolutionManager>();
            solutionManager.SetupGet(x => x.SolutionDirectory).Returns("z:\\SomeRandomPath");
            var settings = new Mock<ISettings>();

            AddService<IVsSolutionManager>(Task.FromResult<object>(solutionManager.Object));
            AddService<ISourceRepositoryProvider>(Task.FromResult<object>(sourceRepositoryProvider.Object));
            AddService<ISettings>(Task.FromResult<object>(settings.Object));

            var serviceActivationOptions = default(ServiceActivationOptions);
            var serviceBroker = new Mock<IServiceBroker>();
            var authorizationService = new AuthorizationServiceClient(Mock.Of<IAuthorizationService>());

            var sharedState = new SharedServiceState();

            var projectManagerService = new Mock<INuGetProjectManagerService>();

            projectManagerService.Setup(x =>
                x.GetInstalledPackagesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>>(_installedPackages.ToList()));

#pragma warning disable ISB001 // Dispose of proxies
            serviceBroker.Setup(x =>
                x.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<INuGetProjectManagerService>(projectManagerService.Object));
#pragma warning restore ISB001 // Dispose of proxies

            return new NuGetPackageSearchService(serviceActivationOptions, serviceBroker.Object, authorizationService, sharedState);
        }
    }
}
