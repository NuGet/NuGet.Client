// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models
{
    public abstract class DetailControlModelTestBase : IClassFixture<LocalPackageSearchMetadataFixture>, IDisposable
    {
        protected readonly LocalPackageSearchMetadataFixture _testData;
        protected readonly PackageItemListViewModel _testViewModel;
        protected readonly JoinableTaskContext _joinableTaskContext;
        protected bool disposedValue = false;

        public DetailControlModelTestBase(LocalPackageSearchMetadataFixture testData)
        {
            _testData = testData;
            var testVersion = new NuGetVersion(0, 0, 1);
            _testViewModel = new PackageItemListViewModel()
            {
                PackageReader = _testData.TestData.PackageReader,
                Version = testVersion,
                InstalledVersion = testVersion,
            };

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

    public class PackageDetailControlModelTests : DetailControlModelTestBase
    {
        private readonly PackageDetailControlModel _testInstance;

        public PackageDetailControlModelTests(LocalPackageSearchMetadataFixture testData)
            : base(testData)
        {
            var solMgr = new Mock<ISolutionManager>();
            _testInstance = new PackageDetailControlModel(
                solutionManager: solMgr.Object,
                projects: new List<ProjectContextInfo>());

            _testInstance.SetCurrentPackage(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void PackageReader_NotNull()
        {
            Assert.NotNull(_testInstance.PackageReader);

            Func<PackageReaderBase> lazyReader = _testInstance.PackageReader;

            PackageReaderBase reader = lazyReader();
            Assert.IsType(typeof(PackageArchiveReader), reader);
        }
    }

    public class PackageSolutionDetailControlModelTests : DetailControlModelTestBase
    {
        private PackageSolutionDetailControlModel _testInstance;

        public PackageSolutionDetailControlModelTests(LocalPackageSearchMetadataFixture testData)
            : base(testData)
        {
            var solMgr = new Mock<ISolutionManager>();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                _testInstance = await PackageSolutionDetailControlModel.CreateAsync(
                    solutionManager: solMgr.Object,
                    projects: new List<ProjectContextInfo>(),
                    packageManagerProviders: new List<IVsPackageManagerProvider>(),
                    CancellationToken.None);
            });

            _testInstance.SetCurrentPackage(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void PackageReader_NotNull()
        {
            Assert.NotNull(_testInstance.PackageReader);

            Func<PackageReaderBase> lazyReader = _testInstance.PackageReader;

            PackageReaderBase reader = lazyReader();
            Assert.IsType(typeof(PackageArchiveReader), reader);
        }
    }
}
