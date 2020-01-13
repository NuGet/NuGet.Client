// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;
using Moq;
using NuGet.ProjectManagement;
using NuGet.Test.Utility;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using System.Threading;
using System;
using NuGet.Versioning;

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
            _testViewModel = new PackageItemListViewModel()
            {
                LocalPackageInfo = _testData.TestData.LocalPackageInfo,
                Version = new NuGetVersion(0, 0, 1),
                InstalledVersion = new NuGetVersion(0, 0, 1)
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
                    // TODO: dispose managed state (managed objects).
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
        private readonly PackageDetailControlModel _testIntance;

        public PackageDetailControlModelTests(LocalPackageSearchMetadataFixture testData)
            : base(testData)
        {
            var solMgr = new Mock<ISolutionManager>();
            _testIntance = new PackageDetailControlModel(
                solutionManager: solMgr.Object,
                nugetProjects: new List<NuGetProject>());

            _testIntance.SetCurrentPackage(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void PackageArchiveReader_NotNull()
        {
            Assert.NotNull(_testIntance.PackageArchiveReader);
        }
    }

    public class PackageSolutionDetailControlModelTests : DetailControlModelTestBase
    {
        private readonly PackageSolutionDetailControlModel _testIntance;

        public PackageSolutionDetailControlModelTests(LocalPackageSearchMetadataFixture testData)
            : base(testData)
        {
            var solMgr = new Mock<ISolutionManager>();
            _testIntance = new PackageSolutionDetailControlModel(
                solutionManager: solMgr.Object,
                projects: new List<NuGetProject>(),
                packageManagerProviders: new List<IVsPackageManagerProvider>());

            _testIntance.SetCurrentPackage(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void PackageArchiveReader_NotNull()
        {
            Assert.NotNull(_testIntance.PackageArchiveReader);
        }
    }
}
