// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.ProjectManagement;
using NuGet.Test.Utility;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common.Telemetry;
using NuGet.VisualStudio.Common.Telemetry.PowerShell;
using Xunit;
using PSCommand = System.Management.Automation.Runspaces.Command;

namespace NuGetConsole.Host.PowerShell.Test
{
    [Collection(MockedVS.Collection)]
    public class NuGetPowerShellBaseCommandTests : IAsyncServiceProvider
    {
        public NuGetPowerShellBaseCommandTests(GlobalServiceProvider globalServiceProvider)
        {
            globalServiceProvider.Reset();
            var componentModel = new Mock<IComponentModel>();
            componentModel.Setup(x => x.GetService<ISettings>()).Returns(Mock.Of<ISettings>());
            globalServiceProvider.AddService(typeof(SComponentModel), componentModel.Object);
            ServiceLocator.InitializePackageServiceProvider(this);
        }

        [Theory]
        [InlineData("init.ps1", false, 1)]
        [InlineData("INIT.PS1", false, 1)]
        [InlineData("init.ps1", true, 1)]
        [InlineData("install.ps1", false, 0)]
        [InlineData("uninstall.ps1", false, 0)]
        [InlineData("notinit.ps1", false, 0)]
        public void ExecutePSScriptInternal_CountsOnlyInitScriptAttempts(string scriptName, bool scriptThrows, int expectedCount)
        {
            using var directory = TestDirectory.Create();
            var scriptPath = Path.Combine(directory.Path, scriptName);
            File.WriteAllText(scriptPath, "$global:PackageScriptExecuted = $true" + (scriptThrows ? "; throw 'script failure'" : ""));

            var sessionState = InitialSessionState.CreateDefault();
            sessionState.Commands.Add(new SessionStateCmdletEntry("Invoke-TestPackageScript", typeof(InvokePackageScriptCommand), null));
            using var runspace = RunspaceFactory.CreateRunspace(new TestPSHost(directory.Path), sessionState);
            runspace.Open();
            TelemetryEvent? telemetryEvent = null;
            var telemetryService = new Mock<INuGetTelemetryService>();
            telemetryService.Setup(s => s.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(e => telemetryEvent = e);
            using var collector = new NuGetPowerShellUsageCollector(telemetryService.Object);
            NuGetPowerShellUsage.RaiseSolutionOpenEvent();
            using var pipeline = runspace.CreatePipeline();
            var command = new PSCommand("Invoke-TestPackageScript");
            command.Parameters.Add("ScriptPath", scriptPath);
            pipeline.Commands.Add(command);

            var results = pipeline.Invoke();
            NuGetPowerShellUsage.RaiseSolutionCloseEvent();

            Assert.True(Assert.IsType<bool>(Assert.Single(results).BaseObject));
            Assert.NotNull(telemetryEvent);
            Assert.Equal(expectedCount, telemetryEvent["PowerShellHost.PmcInitPs1ExecuteCount"]);
            Assert.Equal(0, telemetryEvent["PowerShellHost.PmcExecuteCommandCount"]);
            Assert.Equal(0, telemetryEvent["PowerShellHost.PmuiExecuteCommandCount"]);
        }

        public Task<object?> GetServiceAsync(Type serviceType)
        {
            return Task.FromResult<object?>(null);
        }

        [Cmdlet(VerbsLifecycle.Invoke, "TestPackageScript")]
        public sealed class InvokePackageScriptCommand : NuGetPowerShellBaseCommand
        {
            [Parameter(Mandatory = true)]
            public string ScriptPath { get; set; } = string.Empty;

            protected override bool IsLoggingTimeDisabled => true;

            protected override void ProcessRecordCore()
            {
                ExecutePSScriptInternal(ScriptPath);
                WriteObject(SessionState.PSVariable.GetValue("PackageScriptExecuted"));
            }

            protected override void LogCore(MessageLevel level, string formattedMessage)
            {
            }
        }
    }
}
