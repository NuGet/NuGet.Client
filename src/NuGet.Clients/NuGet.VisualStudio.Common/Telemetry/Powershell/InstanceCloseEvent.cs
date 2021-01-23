// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry.PowerShell
{
    public class InstanceCloseEvent : TelemetryEvent
    {
        public InstanceCloseEvent(
            int PmcExecuteCommandCount,
            int PmcWindowLoadCount,
            int PmuiExecuteCommandCount,
            int PmcPowerShellLoadedSolutionCount,
            int PmuiPowerShellLoadedSolutionCount,
            bool ReOpenAtStart,
            int SolutionCount
            ) : base(NuGetPowerShellUsageCollector.InstanceClose)
        {
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcExecuteCommandCount] = PmcExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcWindowLoadCount] = PmcWindowLoadCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmuiExecuteCommandCount] = PmuiExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcPowerShellLoadedSolutionCount] = PmcPowerShellLoadedSolutionCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmuiPowerShellLoadedSolutionCount] = PmuiPowerShellLoadedSolutionCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.ReOpenAtStart] = ReOpenAtStart;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.SolutionCount] = SolutionCount;
        }
    }
}
