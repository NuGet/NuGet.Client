// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio.Common.Telemetry.PowerShell
{
    public class InstanceCloseEvent : TelemetryEvent
    {
        public InstanceCloseEvent(
            int pmcExecuteCommandCount,
            int pmcWindowLoadCount,
            int pmuiExecuteCommandCount,
            int pmcPowerShellLoadedSolutionCount,
            int pmuiPowerShellLoadedSolutionCount,
            bool reOpenAtStart,
            int solutionCount
            ) : base(NuGetPowerShellUsageCollector.InstanceClose)
        {
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcExecuteCommandCount] = pmcExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcWindowLoadCount] = pmcWindowLoadCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmuiExecuteCommandCount] = pmuiExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcPowerShellLoadedSolutionCount] = pmcPowerShellLoadedSolutionCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmuiPowerShellLoadedSolutionCount] = pmuiPowerShellLoadedSolutionCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.ReOpenAtStart] = reOpenAtStart;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.SolutionCount] = solutionCount;
        }
    }
}
