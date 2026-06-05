// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;
using PSCommand = System.Management.Automation.Runspaces.Command;

namespace NuGetConsole.Host.PowerShell.Test
{
    [Collection(MockedVS.Collection)]
    public class GetPackageCommandTests : IAsyncServiceProvider
    {
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>();
        private readonly Mock<IComponentModel> _componentModel;
        private readonly Mock<IVsSolutionManager> _solutionManager;

        public GetPackageCommandTests(GlobalServiceProvider globalServiceProvider)
        {
            globalServiceProvider.Reset();

            _solutionManager = new Mock<IVsSolutionManager>();
            _solutionManager.SetupGet(x => x.SolutionDirectory).Returns(@"C:\test");
            _solutionManager.Setup(x => x.GetNuGetProjectsAsync())
                .ReturnsAsync(Enumerable.Empty<NuGetProject>());

            _componentModel = new Mock<IComponentModel>();
            _componentModel.Setup(x => x.GetService<ISettings>()).Returns(Mock.Of<ISettings>());
            _componentModel.Setup(x => x.GetService<IVsSolutionManager>()).Returns(_solutionManager.Object);
            _componentModel.Setup(x => x.GetService<ISourceControlManagerProvider>()).Returns(Mock.Of<ISourceControlManagerProvider>());
            _componentModel.Setup(x => x.GetService<ICommonOperations>()).Returns(Mock.Of<ICommonOperations>());
            _componentModel.Setup(x => x.GetService<IPackageRestoreManager>()).Returns(Mock.Of<IPackageRestoreManager>());
            _componentModel.Setup(x => x.GetService<IDeleteOnRestartManager>()).Returns(Mock.Of<IDeleteOnRestartManager>());
            _componentModel.Setup(x => x.GetService<IRestoreProgressReporter>()).Returns(Mock.Of<IRestoreProgressReporter>());

            globalServiceProvider.AddService(typeof(SComponentModel), _componentModel.Object);

            ServiceLocator.InitializePackageServiceProvider(this);
        }

        [Fact]
        public async Task GetPackageListAvailable_WithLocalSource_ReturnsPackagesAsync()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("TestPackageA", "1.0.0");
            await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA);

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new PackageSource(pathContext.PackageSource));
            _componentModel.Setup(x => x.GetService<ISourceRepositoryProvider>()).Returns(sourceRepositoryProvider);

            using var fixture = new CmdletRunspaceFixture(activeSource: pathContext.PackageSource);

            // Act
            var results = fixture.Invoke(
                "Get-Package",
                new Dictionary<string, object>
                {
                    { "ListAvailable", true },
                    { "Source", pathContext.PackageSource },
                });

            // Assert
            results.Should().ContainSingle();
            var package = (PowerShellRemotePackage)results[0].BaseObject;
            package.Id.Should().Be("TestPackageA");
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
        /// Encapsulates runspace and host setup for invoking NuGet PowerShell cmdlets in tests.
        /// </summary>
        private sealed class CmdletRunspaceFixture : IDisposable
        {
            private readonly Runspace _runspace;

            public CmdletRunspaceFixture(string activeSource = "https://api.nuget.org/v3/index.json")
            {
                var host = new TestPSHost(activeSource);
                var initialSessionState = InitialSessionState.CreateDefault();
                initialSessionState.Commands.Add(
                    new SessionStateCmdletEntry("Get-Package", typeof(GetPackageCommand), null));

                _runspace = RunspaceFactory.CreateRunspace(host, initialSessionState);
                _runspace.Open();
            }

            public IList<PSObject> Invoke(string cmdletName, Dictionary<string, object> parameters)
            {
                using var pipeline = _runspace.CreatePipeline();
                var cmd = new PSCommand(cmdletName);
                foreach (var kvp in parameters)
                {
                    cmd.Parameters.Add(kvp.Key, kvp.Value);
                }
                pipeline.Commands.Add(cmd);
                return pipeline.Invoke().ToList();
            }

            public void Dispose()
            {
                _runspace.Close();
                _runspace.Dispose();
            }
        }

        /// <summary>
        /// Minimal PSHost that provides PrivateData with properties expected by NuGet cmdlets.
        /// </summary>
        private sealed class TestPSHost : PSHost
        {
            private readonly Guid _instanceId = Guid.NewGuid();
            private readonly PSObject _privateData;

            public TestPSHost(string activeSource)
            {
                _privateData = new PSObject();
                _privateData.Properties.Add(new PSNoteProperty("activePackageSource", activeSource));
                _privateData.Properties.Add(new PSNoteProperty("CancellationTokenKey", CancellationToken.None));
            }

            public override CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
            public override CultureInfo CurrentUICulture => CultureInfo.InvariantCulture;
            public override Guid InstanceId => _instanceId;
            public override string Name => "TestNuGetHost";
            public override PSObject PrivateData => _privateData;
            public override PSHostUserInterface UI => null;
            public override Version Version => new Version(1, 0);

            public override void EnterNestedPrompt() { }
            public override void ExitNestedPrompt() { }
            public override void NotifyBeginApplication() { }
            public override void NotifyEndApplication() { }
            public override void SetShouldExit(int exitCode) { }
        }
    }
}
