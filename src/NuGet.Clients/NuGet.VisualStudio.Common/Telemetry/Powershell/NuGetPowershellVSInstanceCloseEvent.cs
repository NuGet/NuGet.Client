// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using TelemetryConst = NuGet.VisualStudio.Telemetry.VSPowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    internal class NuGetPowershellVSInstanceCloseEvent : TelemetryEvent
    {
        // Emitted first time powershell gets loaded, so we can detect if VS crash while powershell in use.
        public NuGetPowershellVSInstanceCloseEvent(
            int nugetpmcexecutecommandcount,
            int nugetpmcwindowloadcount,
            int nugetpmuiexecutecommandcount,
            int pmcpowershellloadedsolutioncount,
            int pmuipowershellloadedsolutioncount,
            bool reopenatstart,
            int solutioncount) : base(TelemetryConst.NuGetVSInstanceClose)
        {
            base[TelemetryConst.NuGetPowershellPrefix + "nugetpmcexecutecommandcount"] = nugetpmcexecutecommandcount;
            base[TelemetryConst.NuGetPowershellPrefix + "nugetpmcwindowloadcount"] = nugetpmcwindowloadcount;
            base[TelemetryConst.NuGetPowershellPrefix + "nugetpmuiexecutecommandcount"] = nugetpmuiexecutecommandcount;
            base[TelemetryConst.NuGetPowershellPrefix + "pmcpowershellloadedsolutioncount"] = pmcpowershellloadedsolutioncount;
            base[TelemetryConst.NuGetPowershellPrefix + "pmuipowershellloadedsolutioncount"] = pmuipowershellloadedsolutioncount;
            base[TelemetryConst.NuGetPowershellPrefix + "reopenatstart"] = reopenatstart;
            base[TelemetryConst.NuGetPowershellPrefix + "solutioncount"] = solutioncount;
        }
    }
}
