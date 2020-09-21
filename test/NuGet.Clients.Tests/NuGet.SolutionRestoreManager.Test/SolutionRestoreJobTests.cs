// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager.Test
{
    [Collection(MockedVS.Collection)]
    public class SolutionRestoreJobTests
    {
        private GlobalServiceProvider _globalProvider;

        public SolutionRestoreJobTests(GlobalServiceProvider sp)
        {
            sp.Reset();
            _globalProvider = sp;
        }

        [Fact]
        public async Task Simple_SuceededsAsync()
        {
            var restoreMan = Mock.Of<IPackageRestoreManager>();
            _globalProvider.AddService(typeof(IPackageRestoreManager), restoreMan);
            var slnMan = Mock.Of<IVsSolutionManager>();
            _globalProvider.AddService(typeof(IVsSolutionManager), slnMan);
            SourceRepositoryProvider sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            _globalProvider.AddService(typeof(ISourceRepositoryProvider), sourceRepositoryProvider);

            var restoreChecker = Mock.Of<ISolutionRestoreChecker>();
            var eventsPublisher = Mock.Of<IRestoreEventsPublisher>();
            var settings = Mock.Of<ISettings>();

            Mock.Get(settings)
                .Setup(x => x.GetSection("packageRestore"))
                .Returns(() => new VirtualSettingSection("packageRestore",
                    new AddItem("automatic", bool.TrueString)));

            var consoleProvider = Mock.Of<IOutputConsoleProvider>();
            var logger = new RestoreOperationLogger(new Lazy<IOutputConsoleProvider>(() => consoleProvider));

            var job = new SolutionRestoreJob(
                asyncServiceProvider: AsyncServiceProvider.GlobalProvider,
                packageRestoreManager: restoreMan,
                solutionManager: slnMan,
                sourceRepositoryProvider: sourceRepositoryProvider,
                restoreEventsPublisher: eventsPublisher,
                settings: settings,
                solutionRestoreChecker: restoreChecker);

            var restoreRequest = new SolutionRestoreRequest(
                forceRestore: true,
                RestoreOperationSource.OnBuild);
            var restoreJobContext = new SolutionRestoreJobContext();

            await job.ExecuteAsync(
                request: restoreRequest,
                jobContext: restoreJobContext,
                logger: logger,
                isSolutionLoadRestore: true,
                token: CancellationToken.None);

            Assert.Equal(NuGetOperationStatus.NoOp, job.Status);
        }
    }
}
