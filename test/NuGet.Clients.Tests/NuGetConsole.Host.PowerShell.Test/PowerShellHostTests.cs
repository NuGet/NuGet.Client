// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGetConsole.Host.PowerShell.Implementation;
using Test.Utility;
using Xunit;

namespace NuGetConsole.Host.PowerShell.Test
{
    [Collection(MockedVS.Collection)]
    public class PowerShellHostTests
    {
        public PowerShellHostTests(GlobalServiceProvider globalServiceProvider)
        {
            globalServiceProvider.Reset();
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(ThreadHelper.JoinableTaskFactory);
        }

        [Fact]
        public async Task Initialize_WaitsForFeatureFlags_AndDoesNotCreateRunspaceOnFailureAsync()
        {
            var initializationStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var initialization = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var featureFlagService = new Mock<INuGetFeatureFlagService>(MockBehavior.Strict);
            featureFlagService.Setup(x => x.InitializeAsync())
                .Callback(() => initializationStarted.TrySetResult(true))
                .Returns(initialization.Task);
            var sourceRepositoryProvider = new Mock<ISourceRepositoryProvider>();
            sourceRepositoryProvider.Setup(x => x.GetRepositories()).Returns(Array.Empty<SourceRepository>());
            sourceRepositoryProvider.SetupGet(x => x.PackageSourceProvider).Returns(Mock.Of<IPackageSourceProvider>());
            var componentModel = new Mock<IComponentModel>(MockBehavior.Strict);
            componentModel.Setup(x => x.GetService<ISourceRepositoryProvider>()).Returns(sourceRepositoryProvider.Object);
            componentModel.Setup(x => x.GetService<INuGetFeatureFlagService>()).Returns(featureFlagService.Object);
            var serviceProvider = new Mock<IAsyncServiceProvider>();
            serviceProvider.Setup(x => x.GetServiceAsync(typeof(SComponentModel))).ReturnsAsync(componentModel.Object);
            ServiceLocator.InitializePackageServiceProvider(serviceProvider.Object);

            var runspaceManager = new Mock<IRunspaceManager>(MockBehavior.Strict);
            var environment = new TestEnvironmentVariableReader(new Dictionary<string, string>());
            using var host = new SyncPowerShellHost(
                "Test host", Mock.Of<IRestoreEvents>(), runspaceManager.Object, environment);
            var console = Mock.Of<IConsole>();

            Task initializeHost = Task.Run(() => host.Initialize(console));
            try
            {
                Task started = await Task.WhenAny(
                    initializationStarted.Task, initializeHost, Task.Delay(TimeSpan.FromSeconds(10)));
                started.Should().BeSameAs(initializationStarted.Task);
                initializeHost.IsCompleted.Should().BeFalse();
                runspaceManager.VerifyNoOtherCalls();

                initialization.SetException(new InvalidOperationException("Feature flag initialization failed."));
            }
            finally
            {
                initialization.TrySetCanceled();
                Task completed = await Task.WhenAny(initializeHost, Task.Delay(TimeSpan.FromSeconds(10)));
                completed.Should().BeSameAs(initializeHost, "host initialization must not hang");
                await initializeHost;
            }

            host.IsInitializedSuccessfully.Should().BeFalse();
            host.IsCommandEnabled.Should().BeFalse();
            runspaceManager.VerifyNoOtherCalls();
        }
    }
}
