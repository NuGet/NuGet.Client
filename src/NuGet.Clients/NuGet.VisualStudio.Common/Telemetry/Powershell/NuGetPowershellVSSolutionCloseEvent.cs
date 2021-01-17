// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using TelemetryConst = NuGet.VisualStudio.Telemetry.VSPowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    internal class NuGetPowershellVSSolutionCloseEvent : TelemetryEvent
    {
        // Emitted first time powershell gets loaded, so we can detect if VS crash while powershell in use.
        public NuGetPowershellVSSolutionCloseEvent() : this(
            FirstTimeLoadedFromPMC: false,
            FirstTimeLoadedFromPMUI: false,
            InitPs1LoadedFromPMCFirst: false,
            InitPs1LoadPMC: false,
            InitPs1LoadPMUI: false,
            LoadedFromPMC: false,
            LoadedFromPMUI: false,
            NuGetCommandUsed: false,
            NuGetPMCExecuteCommandCount: 0,
            NuGetPMCWindowLoadCount: 0,
            NuGetPMUIExecuteCommandCount: 0,
            SolutionLoaded: false)
        { }

        public NuGetPowershellVSSolutionCloseEvent(
            bool FirstTimeLoadedFromPMC,
            bool FirstTimeLoadedFromPMUI,
            bool InitPs1LoadedFromPMCFirst,
            bool InitPs1LoadPMC,
            bool InitPs1LoadPMUI,
            bool LoadedFromPMC,
            bool LoadedFromPMUI,
            bool NuGetCommandUsed,
            int NuGetPMCExecuteCommandCount,
            int NuGetPMCWindowLoadCount,
            int NuGetPMUIExecuteCommandCount,
            bool SolutionLoaded) : base(TelemetryConst.NuGetVSSolutionClose)
        {
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.FirstTimeLoadedFromPMC] = FirstTimeLoadedFromPMC;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.FirstTimeLoadedFromPMUI] = FirstTimeLoadedFromPMUI;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.InitPs1LoadedFromPMCFirst] = InitPs1LoadedFromPMCFirst;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.InitPs1LoadPMC] = InitPs1LoadPMC;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.InitPs1LoadPMUI] = InitPs1LoadPMUI;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.LoadedFromPMC] = LoadedFromPMC;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.LoadedFromPMUI] = LoadedFromPMUI;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetCommandUsed] = NuGetCommandUsed;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCExecuteCommandCount] = NuGetPMCExecuteCommandCount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCWindowLoadCount] = NuGetPMCWindowLoadCount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMUIExecuteCommandCount] = NuGetPMUIExecuteCommandCount;
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.SolutionLoaded] = SolutionLoaded;
        }
    }
}
