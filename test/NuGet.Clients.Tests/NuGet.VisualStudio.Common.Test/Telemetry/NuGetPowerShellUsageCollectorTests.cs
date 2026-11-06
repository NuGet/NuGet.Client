// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using NuGet.Common;
using NuGet.VisualStudio.Common.Telemetry;
using NuGet.VisualStudio.Common.Telemetry.PowerShell;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Common.Test.Telemetry
{
    [CollectionDefinition(nameof(NuGetPowerShellUsageCollection), DisableParallelization = true)]
    public sealed class NuGetPowerShellUsageCollection
    {
    }

    [Collection(nameof(NuGetPowerShellUsageCollection))]
    public class NuGetPowerShellUsageCollectorTests
    {
        private const string PmcCommands = "PowerShellHost.PmcExecuteCommandCount";
        private const string PmcInitScripts = "PowerShellHost.PmcInitPs1ExecuteCount";
        private const string PmuiCommands = "PowerShellHost.PmuiExecuteCommandCount";

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 0)]
        public void InitPs1Execute_CountsOnlyAutomaticPmcExecutions(bool isPmc, int expectedCount)
        {
            var events = new List<TelemetryEvent>();
            var telemetryService = new Mock<INuGetTelemetryService>();
            telemetryService.Setup(s => s.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(events.Add);
            using var collector = new NuGetPowerShellUsageCollector(telemetryService.Object);

            NuGetPowerShellUsage.RaiseSolutionOpenEvent();
            NuGetPowerShellUsage.RaiseInitPs1ExecuteEvent(isPMC: isPmc);
            NuGetPowerShellUsage.RaiseInitPs1ExecuteEvent(isPMC: isPmc);
            NuGetPowerShellUsage.RaiseSolutionCloseEvent();
            var instanceEvent = InstanceCloseTelemetryEmitter.CreateTelemetryEvent();

            var solutionEvent = Assert.Single(events);
            Assert.Equal("SolutionClose", solutionEvent.Name);
            Assert.Equal(expectedCount, solutionEvent[PmcInitScripts]);
            Assert.Equal(expectedCount, instanceEvent[PmcInitScripts]);
            Assert.Equal(0, solutionEvent[PmcCommands]);
            Assert.Equal(0, instanceEvent[PmcCommands]);
            Assert.Equal(0, solutionEvent[PmuiCommands]);
            Assert.Equal(0, instanceEvent[PmuiCommands]);
        }

        [Fact]
        public void SolutionClose_ResetsSolutionCountAndRetainsInstanceCount()
        {
            var events = new List<TelemetryEvent>();
            var telemetryService = new Mock<INuGetTelemetryService>();
            telemetryService.Setup(s => s.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(events.Add);
            using var collector = new NuGetPowerShellUsageCollector(telemetryService.Object);

            NuGetPowerShellUsage.RaiseSolutionOpenEvent();
            NuGetPowerShellUsage.RaiseInitPs1ExecuteEvent(isPMC: true);
            NuGetPowerShellUsage.RaiseSolutionCloseEvent();
            NuGetPowerShellUsage.RaiseSolutionOpenEvent();
            NuGetPowerShellUsage.RaiseInitPs1ExecuteEvent(isPMC: true);
            NuGetPowerShellUsage.RaiseInitPs1ExecuteEvent(isPMC: true);
            NuGetPowerShellUsage.RaiseCommandExecuteEvent(isPMC: true);
            NuGetPowerShellUsage.RaiseSolutionCloseEvent();
            NuGetPowerShellUsage.RaiseSolutionOpenEvent();
            NuGetPowerShellUsage.RaiseSolutionCloseEvent();
            var instanceEvent = InstanceCloseTelemetryEmitter.CreateTelemetryEvent();

            Assert.Equal(3, events.Count);
            Assert.Equal(1, events[0][PmcInitScripts]);
            Assert.Equal(2, events[1][PmcInitScripts]);
            Assert.Equal(0, events[2][PmcInitScripts]);
            Assert.Equal(0, events[0][PmcCommands]);
            Assert.Equal(1, events[1][PmcCommands]);
            Assert.Equal(0, events[2][PmcCommands]);
            Assert.Equal(3, instanceEvent[PmcInitScripts]);
            Assert.Equal(1, instanceEvent[PmcCommands]);
        }
    }
}
