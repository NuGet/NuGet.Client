// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class NuGetUITests : IDisposable
    {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly TestDirectory _testDirectory;

        public NuGetUITests()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            _joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_joinableTaskContext.Factory);

            _testDirectory = TestDirectory.Create();
        }

        public void Dispose()
        {
            _joinableTaskContext?.Dispose();
            _testDirectory.Dispose();
        }

        [Fact]
        public void ShowError_WhenArgumentIsSignatureExceptionWithNullResults_DoesNotThrow()
        {
            var exception = new SignatureException(message: "a");

            Assert.Null(exception.Results);

            using (NuGetUI ui = CreateNuGetUI())
            {
                ui.ShowError(exception);
            }
        }

        private NuGetUI CreateNuGetUI()
        {
            var uiContext = CreateNuGetUIContext();

            return new NuGetUI(
                Mock.Of<ICommonOperations>(),
                new NuGetUIProjectContext(
                    Mock.Of<ICommonOperations>(),
                    Mock.Of<INuGetUILogger>(),
                    Mock.Of<ISourceControlManagerProvider>()),
                Mock.Of<INuGetUILogger>(),
                uiContext);
        }

        private NuGetUIContext CreateNuGetUIContext()
        {
            var sourceRepositoryProvider = Mock.Of<ISourceRepositoryProvider>();
            var packageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                Mock.Of<ISettings>(),
                _testDirectory.Path);

            return new NuGetUIContext(
                sourceRepositoryProvider,
                Mock.Of<IServiceBroker>(),
                Mock.Of<IVsSolutionManager>(),
                new NuGetSolutionManagerServiceWrapper(),
                packageManager,
                new UIActionEngine(
                    sourceRepositoryProvider,
                    packageManager,
                    Mock.Of<INuGetLockService>()),
                Mock.Of<IPackageRestoreManager>(),
                Mock.Of<IOptionsPageActivator>(),
                Mock.Of<IUserSettingsManager>(),
                Enumerable.Empty<IVsPackageManagerProvider>());
        }
    }
}
