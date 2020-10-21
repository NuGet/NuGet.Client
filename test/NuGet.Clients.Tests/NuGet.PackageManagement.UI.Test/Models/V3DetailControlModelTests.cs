// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models
{
    public abstract class V3DetailControlModelTestBase : IClassFixture<V3PackageSearchMetadataFixture>, IDisposable
    {
        protected readonly V3PackageSearchMetadataFixture _testData;
        protected readonly PackageItemListViewModel _testViewModel;
        protected readonly JoinableTaskContext _joinableTaskContext;
        protected readonly MultiSourcePackageMetadataProvider _metadataProvider;
        protected bool disposedValue = false;

        public V3DetailControlModelTestBase(V3PackageSearchMetadataFixture testData)
        {
            _testData = testData;

            // The versions pre-baked into the view model provide data for the first step of metadata extraction
            // which fails (null) in a V3 scenario--they need to be extracted using a metadata provider (below)
            var testVersion = new NuGetVersion(0, 0, 1);
            var testVersions = new List<VersionInfo>() {
                new VersionInfo(new NuGetVersion(0, 0, 1)),
                new VersionInfo(new NuGetVersion(0, 0, 2))
            };
            _testViewModel = new PackageItemListViewModel()
            {
                Version = testVersion,
                Versions = new Lazy<Task<IEnumerable<VersionInfo>>>(() => Task.FromResult<IEnumerable<VersionInfo>>(testVersions)),
                InstalledVersion = testVersion,
            };

            var resourceProvider = new MockNuGetResourceProvider();
            var metadataResource = new MockPackageMetadataResource(_testData.TestData);
            var repository = new MockSourceRepository<PackageMetadataResource>(
                new PackageSource("nuget.psm.test"),
                new List<INuGetResourceProvider>() { resourceProvider },
                metadataResource);
            var repositories = new List<SourceRepository>() { repository };
            _metadataProvider = new MultiSourcePackageMetadataProvider(
                repositories,
                optionalLocalRepository: null,
                optionalGlobalLocalRepositories: null,
                logger: new Mock<ILogger>().Object);

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            _joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_joinableTaskContext.Factory);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _joinableTaskContext?.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
    }

    public class V3PackageDetailControlModelTests : V3DetailControlModelTestBase
    {
        private readonly PackageDetailControlModel _testInstance;

        public V3PackageDetailControlModelTests(V3PackageSearchMetadataFixture testData)
            : base(testData)
        {
            var solMgr = new Mock<INuGetSolutionManagerService>();
            _testInstance = new PackageDetailControlModel(
                Mock.Of<IServiceBroker>(),
                solutionManager: solMgr.Object,
                Array.Empty<IProjectContextInfo>());
            _testInstance.SetCurrentPackage(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public async void ViewModelMarkedVulnerableWhenMetadataHasVulnerability_Flagged()
        {
            await _testInstance.LoadPackageMetadataAsync(_metadataProvider, CancellationToken.None);
            Assert.True(_testInstance.IsPackageVulnerable);
        }

        [Fact]
        public async void MaxVulnerabilitySeverityWhenMetadataHasVulnerability_Calculated()
        {
            await _testInstance.LoadPackageMetadataAsync(_metadataProvider, CancellationToken.None);
            Assert.Equal(_testInstance.PackageVulnerabilityMaxSeverity, _testData.TestData.Vulnerabilities.Max(v => v.Severity));
        }
    }

    internal class MockNuGetResourceProvider : INuGetResourceProvider
    {
        public Type ResourceType => typeof(PackageMetadataResource);

        public string Name => "TestPackageMetadataResourceProvider";

        public IEnumerable<string> Before => new List<string>() { "ccc" };

        public IEnumerable<string> After => new List<string>() { "aaa" };

        public Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }

    internal class MockPackageMetadataResource : PackageMetadataResource
    {
        IEnumerable<IPackageSearchMetadata> _metadata;

        public MockPackageMetadataResource(IPackageSearchMetadata metadata)
        {
            _metadata = new List<IPackageSearchMetadata> { metadata };
        }

        public override Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            SourceCacheContext sourceCacheContext,
            ILogger log,
            CancellationToken token)
        {
            return Task.FromResult(_metadata);
        }

        public override Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity package, SourceCacheContext sourceCacheContext, ILogger log, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }

    internal class MockSourceRepository<T> : SourceRepository
    {
        T _metadataResource;

        public MockSourceRepository(PackageSource source, IEnumerable<INuGetResourceProvider> providers, T metadataReource)
            : base(source, providers)
        {
            _metadataResource = metadataReource;
        }

        public override Task<TResource> GetResourceAsync<TResource>(CancellationToken token)
        {
            if (typeof(TResource) == typeof(T))
            {
                return Task.FromResult(_metadataResource as TResource);
            }

            return Task.FromResult(default(TResource));
        }
    }
}
