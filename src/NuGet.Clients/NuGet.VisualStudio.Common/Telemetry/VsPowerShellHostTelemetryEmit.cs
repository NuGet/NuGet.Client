// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;
using NuGet.Common.Telemetry;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(VsPowerShellHostTelemetryEmit))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsPowerShellHostTelemetryEmit : VsInstanceTelemetryConsts
    {
        private readonly object _telemetryLock = new object();
        private bool _isTelemetryEmitted;
        private int _pmcExecutedCount;
        private int _nonPmcExecutedCount;
        private readonly Lazy<INuGetTelemetryAggregator> _nugetTelemetryAggregate;

        // There are 8 bit in byte.
        // 0 - not used
        // 1 - not used
        // 2 - If nuget command used during current VS solution session.
        // 3 - If init.ps1 is loaded during current VS solution session.
        // 4 - First time load from PMUI
        // 5 - First time load PMC
        // 6 - LoadedFromPMUI: Indicates powershell host for PMUI already created, and stays that way until VS close.
        // 7 - LoadedFromPMC: Indicates powershell host for PMC already created, and stays that way until VS close.
        private static byte PowerShellHostInstances;

        public VsPowerShellHostTelemetryEmit()
        {
            _nugetTelemetryAggregate = new Lazy<INuGetTelemetryAggregator>(() => ServiceLocator.GetInstanceSafe<INuGetTelemetryAggregator>());
        }

        public void CheckInitOrigin(bool isPMC)
        {
            lock (_telemetryLock)
            {
                //There is edge case where PMC is opened but user doesn't execute any command on it.
                if (isPMC)
                {
                    if ((PowerShellHostInstances & 0b00000001) == 0)
                    {
                        // First time load PMC
                        PowerShellHostInstances |= 0b00000100;
                    }

                    // LoadedFromPMC
                    PowerShellHostInstances |= 0b00000001;
                }
                else
                {
                    if ((PowerShellHostInstances & 0b00000010) == 0)
                    {
                        // First time load from PMUI
                        PowerShellHostInstances |= 0b00001000;
                    }

                    // LoadedFromPMUI
                    PowerShellHostInstances |= 0b00000010;
                }
            }
        }

        public void IncreaseCommandCounter(bool isPMC)
        {
            if (isPMC)
            {
                lock (_telemetryLock)
                {
                    // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                    // For PMC all installation done in one pass so no double counting.
                    _pmcExecutedCount++;
                }
            }
            else
            {
                lock (_telemetryLock)
                {
                    // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                    // This one is called for both init.ps1 and install.ps1 seperately.
                    // For MSBuildNuGetProject projects install.ps1 can event furthure duplicate counted: MSBuildNuGetProject.cs#L377 - L396
                    // Also this concern valid for dependent packages with *.ps1 files.
                    _nonPmcExecutedCount++;
                }
            }
        }

        public void HandleSolutionOpenedEmit()
        {
            if (_pmcExecutedCount > 0)
            {
                // PMC used before any solution is loaded, let's emit what we have for nugetvsinstanceclose event aggregation before loading a solution.
                EmitPowershellUsageTelemetry(false);
            }

            _isTelemetryEmitted = false;
        }


        public void EmitPowerShellLoadedTelemetry(bool isPMC)
        {
            lock (_telemetryLock)
            {
                // This is PowerShellHost load first time.
                if ((PowerShellHostInstances & 0b00000011) == 0)
                {
                    var telemetryEvent = new TelemetryEvent(NuGetPowerShellLoaded, new Dictionary<string, object>
                                    {
                                        { NugetPowershellPrefix + LoadFromPMC, isPMC}
                                    });

                    TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
                }
            }
        }

        public void EmitPowershellUsageTelemetry(bool withSolution)
        {
            lock (_telemetryLock)
            {
                if (!_isTelemetryEmitted)
                {
                    var telemetryEvent = new TelemetryEvent(PowerShellExecuteCommand, new Dictionary<string, object>
                                    {
                                        { NuGetPMCExecuteCommandCount, _pmcExecutedCount},
                                        { NuGetPMUIExecuteCommandCount, _nonPmcExecutedCount},
                                        { NuGetCommandUsed, (PowerShellHostInstances & 0b00100000) == 0b00100000},
                                        { InitPs1Loaded, (PowerShellHostInstances & 0b00010000) == 0b00010000},
                                        { FirstTimeLoadedFromPMUI, (PowerShellHostInstances & 0b00001000) == 0b00001000},
                                        { FirstTimeLoadedFromPMC, (PowerShellHostInstances & 0b00000100) == 0b00000100},
                                        { LoadedFromPMUI, (PowerShellHostInstances & 0b00000010) == 0b00000010},
                                        { LoadedFromPMC, (PowerShellHostInstances & 0b00000001) == 0b00000001},
                                        { SolutionLoaded, withSolution}
                                    });
                    _nugetTelemetryAggregate.Value.AddSolutionTelemetryEvent(telemetryEvent);

                    _pmcExecutedCount = 0;
                    _nonPmcExecutedCount = 0;
                    _isTelemetryEmitted = true;

                    // Keep other 2 flags for powershell host are created flags, reset all others.
                    PowerShellHostInstances &= 0b00000011;
                }
            }
        }

        public void RecordInitPs1loaded()
        {
            // Init.ps1 is loaded
            PowerShellHostInstances |= 0b00010000;
        }

        public void IsNugetCommand(string commandStr)
        {
            if (!string.IsNullOrWhiteSpace(commandStr))
            {
                string command = commandStr.Trim().ToUpperInvariant();
                string[] commandParts = command.Split(' ');

                if (commandParts.Count() > 1)
                {
                    command = commandParts[0];
                }

                switch (command)
                {
                    case "GET-HELP":
                    case "FIND-PACKAGE":
                    case "GET-PACKAGE":
                    case "INSTALL-PACKAGE":
                    case "UNINSTALL-PACKAGE":
                    case "UPDATE-PACKAGE":
                    case "SYNC-PACKAGE":
                    case "ADD-BINDINGREDIRECT":
                    case "GET-PROJECT":
                    case "REGISTER-TABEXPANSION":
                        // NugetCommand executed
                        PowerShellHostInstances |= 0b00100000;
                        break;
                }
            }
        }
    }
}
