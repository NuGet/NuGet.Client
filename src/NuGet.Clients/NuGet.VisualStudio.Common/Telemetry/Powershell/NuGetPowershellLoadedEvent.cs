// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using TelemetryConst = NuGet.VisualStudio.Telemetry.VSPowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    internal class NuGetPowershellLoadedEvent : TelemetryEvent
    {
        // Emitted first time powershell gets loaded, so we can detect if VS crash while powershell in use.
        public NuGetPowershellLoadedEvent(bool loadedfrompmc) : base(TelemetryConst.NuGetPowerShellLoaded)
        {
            base[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.LoadedFromPMC] = TelemetryConst.LoadedFromPMC;
        }
    }
}
