// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry.PowerShell
{
    public class SolutionCloseEvent : TelemetryEvent
    {
        public SolutionCloseEvent(
            bool FirstTimeLoadedFromPmc,
            bool FirstTimeLoadedFromPmui,
            bool InitPs1LoadedFromPmcFirst,
            bool InitPs1LoadPmc,
            bool InitPs1LoadPmui,
            bool LoadedFromPmc,
            bool LoadedFromPmui,
            bool NuGetCommandUsed,
            int PmcExecuteCommandCount,
            int PmcWindowLoadCount,
            int PmuiExecuteCommandCount,
            bool SolutionLoaded) : base(NuGetPowerShellUsageCollector.SolutionClose)
        {
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.FirstTimeLoadedFromPmc] = FirstTimeLoadedFromPmc;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.FirstTimeLoadedFromPmui] = FirstTimeLoadedFromPmui;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadedFromPmcFirst] = InitPs1LoadedFromPmcFirst;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadPmc] = InitPs1LoadPmc;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadPmui] = InitPs1LoadPmui;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.LoadedFromPmc] = LoadedFromPmc;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.LoadedFromPmui] = LoadedFromPmui;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.NuGetCommandUsed] = NuGetCommandUsed;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcExecuteCommandCount] = PmcExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmcWindowLoadCount] = PmcWindowLoadCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.PmuiExecuteCommandCount] = PmuiExecuteCommandCount;
            base[NuGetPowerShellUsageCollector.PowerShellHost + NuGetPowerShellUsageCollector.SolutionLoaded] = SolutionLoaded;
        }
    }
}
