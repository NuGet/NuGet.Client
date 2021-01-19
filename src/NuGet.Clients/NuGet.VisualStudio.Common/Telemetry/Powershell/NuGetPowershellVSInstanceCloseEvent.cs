// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using TelemetryConst = NuGet.VisualStudio.Telemetry.PowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    internal class NuGetPowershellVSInstanceCloseEvent : TelemetryEvent
    {
        public NuGetPowershellVSInstanceCloseEvent(
            int nugetpmcexecutecommandcount,
            int nugetpmcwindowloadcount,
            int nugetpmuiexecutecommandcount,
            int pmcpowershellloadedsolutioncount,
            int pmuipowershellloadedsolutioncount,
            bool reopenatstart,
            int solutioncount) : base(TelemetryConst.NuGetVSInstanceClose)
        {
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCExecuteCommandCount] = nugetpmcexecutecommandcount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCWindowLoadCount] = nugetpmcwindowloadcount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMUIExecuteCommandCount] = nugetpmuiexecutecommandcount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.PMCPowerShellLoadedSolutionCount] = pmcpowershellloadedsolutioncount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.PMUIPowerShellLoadedSolutionCount] = pmuipowershellloadedsolutioncount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.ReOpenAtStart] = reopenatstart;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.SolutionCount] = solutioncount;
        }
    }
}
