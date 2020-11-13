// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;

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
        private readonly INuGetTelemetryCollector _nugetTelemetryAggregate;

        // There are 8 bits in byte which used as boolean flags.
        // 0 - Did any nuget command execute during current VS solution session?
        // 1 - Did init.ps1 is load during current VS solution session from PMUI?
        // 2 - Did init.ps1 is load during current VS solution session from PMC?
        // 3 - Did init.ps1 load first from PMC or PMUI for above 2 cases?
        // 4 - Did PowerShellHost for PMUI created during current VS solution session first time?
        // 5 - Did PowerShellHost for PMC created during current VS solution session first time?
        // 6 - Did PowerShellHost for PMUI created during current VS instance session?
        // 7 - Did PowerShellHost for PMC created during current VS instance session?
        private static byte PowerShellHostInstances;

        public VsPowerShellHostTelemetryEmit()
        {
            _nugetTelemetryAggregate = ServiceLocator.GetInstance<INuGetTelemetryCollector>();
        }

        public void RecordPSHostInitializeOrigin(bool isPMC)
        {
            lock (_telemetryLock)
            {
                if (isPMC)
                {
                    if (TestAnyBitNotSet(PowerShellHostInstances, 0b00000001))
                    {
                        // First time load PMC
                        PowerShellHostInstances |= 0b00000100;
                    }

                    // LoadedFromPMC
                    PowerShellHostInstances |= 0b00000001;
                }
                else
                {
                    if (TestAnyBitNotSet(PowerShellHostInstances, 0b00000010))
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
            lock (_telemetryLock)
            {
                if (isPMC)
                {
                    // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                    // For PMC all installation done in one pass so no double counting.
                    _pmcExecutedCount++;
                }
                else
                {
                    // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                    // This one is called for both init.ps1 and install.ps1 seperately.
                    // For MSBuildNuGetProject projects install.ps1 can even furthure increase duplicate counting: See MSBuildNuGetProject.cs#L377 - L396
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

        public void HandleSolutionClosingEmit()
        {
            EmitPowershellUsageTelemetry(true);

            // PMC can still used after solution is closed, so reset _isTelemetryEmitted make it possible to remaining telemetry.
            _isTelemetryEmitted = false;
        }

        public void EmitPowerShellLoadedTelemetry(bool isPMC)
        {
            lock (_telemetryLock)
            {
                // This is PowerShellHost load first time, let's emit this to find out later how many VS instance crash after loading powershell.
                if (TestAnyBitNotSet(PowerShellHostInstances, 0b00000011))
                {
                    var telemetryEvent = new TelemetryEvent(NuGetPowerShellLoaded, new Dictionary<string, object>
                                    {
                                        { NuGetPowershellPrefix + LoadedFromPMC, isPMC}
                                    });


                    // Edge case: PMC window can be opened without any solution at all, but sometimes TelemetryActivity.NuGetTelemetryService is not ready yet.
                    // In general we want to emit this telemetry right away.
                    if (TelemetryActivity.NuGetTelemetryService != null)
                    {
                        TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
                    }
                    else
                    {
                        _nugetTelemetryAggregate.AddSolutionTelemetryEvent(telemetryEvent);
                    }
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
                                        { NuGetCommandUsed, TestAllBitsSet(PowerShellHostInstances, 0b10000000) },
                                        { InitPs1LoadPMUI, TestAllBitsSet(PowerShellHostInstances, 0b01000000) },
                                        { InitPs1LoadPMC, TestAllBitsSet(PowerShellHostInstances, 0b00100000) },
                                        { InitPs1LoadedFromPMCFirst, TestAllBitsSet(PowerShellHostInstances, 0b00010000) },
                                        { FirstTimeLoadedFromPMUI, TestAllBitsSet(PowerShellHostInstances, 0b00001000) },
                                        { FirstTimeLoadedFromPMC, TestAllBitsSet(PowerShellHostInstances, 0b00000100) },
                                        { LoadedFromPMUI, TestAllBitsSet(PowerShellHostInstances, 0b00000010) },
                                        { LoadedFromPMC, TestAllBitsSet(PowerShellHostInstances, 0b00000001) },
                                        { SolutionLoaded, withSolution}
                                    });
                    _nugetTelemetryAggregate.AddSolutionTelemetryEvent(telemetryEvent);

                    _pmcExecutedCount = 0;
                    _nonPmcExecutedCount = 0;

                    // Keep 2 flags for current VS instance,but reset all others because they're for current VS session.
                    PowerShellHostInstances &= 0b00000011;
                }

                _isTelemetryEmitted = true;
            }
        }

        public void RecordInitPs1loaded(bool isPMC)
        {
            // Test bit flag for if init.ps1 already loaded from PMC or PMUI
            if (TestAnyBitNotSet(PowerShellHostInstances, 0b01100000) && isPMC)
            {
                // if not then set initialization origin bit
                PowerShellHostInstances |= 0b00010000;
            }

            if (isPMC)
            {
                PowerShellHostInstances |= 0b00100000;
            }
            else
            {
                PowerShellHostInstances |= 0b01000000;
            }
        }

        public void IsNuGetCommand(string commandStr)
        {
            if (TestAllBitsSet(PowerShellHostInstances, 0b10000000))
            {
                // NuGetCommand used and flag is already set
                return;
            }

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
                        PowerShellHostInstances |= 0b10000000;
                        break;
                }
            }
        }

        private bool TestAllBitsSet(byte input, byte mask)
        {
            return (input & mask) == mask;
        }

        private bool TestAnyBitNotSet(byte input, byte mask)
        {
            return (input & mask) == 0;
        }
    }
}
