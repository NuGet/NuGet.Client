// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI.Test.Models
{
    [Collection(MockedVS.Collection)]
    public abstract class V3DetailControlModelTestBase : IClassFixture<V3PackageSearchMetadataFixture>
    {
        protected readonly V3PackageSearchMetadataFixture _testData;
        protected readonly PackageItemListViewModel _testViewModel;

        public V3DetailControlModelTestBase(GlobalServiceProvider sp, V3PackageSearchMetadataFixture testData)
        {
            sp.Reset();
            _testData = testData;

            // The versions pre-baked into the view model provide data for the first step of metadata extraction
            // which fails (null) in a V3 scenario--they need to be extracted using a metadata provider (below)
            var testVersion = new NuGetVersion(0, 0, 1);
            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion(0, 0, 1)),
                new VersionInfoContextInfo(new NuGetVersion(0, 0, 2))
            };

            _testViewModel = new PackageItemListViewModel()
            {
                Id = "nuget.psm",
                Version = testVersion,
                Versions = new Lazy<Task<IReadOnlyCollection<VersionInfoContextInfo>>>(() => Task.FromResult<IReadOnlyCollection<VersionInfoContextInfo>>(testVersions)),
                InstalledVersion = testVersion,
                Sources = new List<PackageSourceContextInfo> { new PackageSourceContextInfo("nuget.psm.test") },
            };
        }
    }

    public class V3PackageDetailControlModelTests : V3DetailControlModelTestBase, IAsyncServiceProvider
    {
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>(TypeEquivalenceComparer.Instance);
        private readonly PackageDetailControlModel _testInstance;
        public V3PackageDetailControlModelTests(GlobalServiceProvider sp, V3PackageSearchMetadataFixture testData)
            : base(sp, testData)
        {
            var solMgr = new Mock<INuGetSolutionManagerService>();

            var packageSearchMetadata = new List<PackageSearchMetadataContextInfo>()
            {
                PackageSearchMetadataContextInfo.Create(_testData.TestData)
            };

            var mockSearchService = new Mock<INuGetSearchService>();
            mockSearchService.Setup(x =>
                x.GetPackageMetadataListAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>>(packageSearchMetadata));

            mockSearchService.Setup(x =>
                x.GetDeprecationMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(null);

            mockSearchService.Setup(x => x.GetPackageMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)>((packageSearchMetadata[0], null)));

            var mockServiceBroker = new Mock<IServiceBroker>();
#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(
                x => x.GetProxyAsync<INuGetSearchService>(
                    NuGetServices.SearchService,
                    It.IsAny<ServiceActivationOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<INuGetSearchService>(mockSearchService.Object));
#pragma warning restore ISB001 // Dispose of proxies

            ServiceLocator.InitializePackageServiceProvider(this);

            _testInstance = new PackageDetailControlModel(
                mockServiceBroker.Object,
                solutionManager: solMgr.Object,
                Array.Empty<IProjectContextInfo>());
            _testInstance.SetCurrentPackageAsync(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void ViewModelMarkedVulnerableWhenMetadataHasVulnerability_Flagged()
        {
            Assert.True(_testInstance.IsPackageVulnerable);
        }

        [Fact]
        public void MaxVulnerabilitySeverityWhenMetadataHasVulnerability_Calculated()
        {
            Assert.Equal(_testInstance.PackageVulnerabilityMaxSeverity, _testData.TestData.Vulnerabilities.Max(v => v.Severity));
        }

        public Task<object> GetServiceAsync(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out Task<object> task))
            {
                return task;
            }

            return Task.FromResult<object>(null);
        }

        /// <summary>
        /// We need an interface that implements both: SVsBrokeredServiceContainer and IBrokeredServiceContainer so we can add it to the service host
        /// </summary>
        public interface IBrokeredServiceContainerMock : SVsBrokeredServiceContainer, IBrokeredServiceContainer
        {
        }

        /// <summary>
        /// Due to embedding the types we need to compare based on IsEquivalentTo
        /// </summary>
        private class TypeEquivalenceComparer : IEqualityComparer<Type>
        {
            public static readonly TypeEquivalenceComparer Instance = new TypeEquivalenceComparer();

            private TypeEquivalenceComparer()
            {
            }

            public bool Equals(Type x, Type y)
            {
                return x.IsEquivalentTo(y);
            }

            public int GetHashCode(Type obj)
            {
                return obj.GUID.GetHashCode();
            }
        }
    }
}
