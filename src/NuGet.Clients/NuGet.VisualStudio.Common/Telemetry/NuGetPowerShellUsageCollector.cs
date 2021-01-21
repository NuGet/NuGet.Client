// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry.Powershell;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(NuGetPowerShellUsageCollector))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class NuGetPowerShellUsageCollector : IDisposable
    {
        // PMC, PMUI powershell telemetry consts
        public const string NuGetPMCExecuteCommandCount = nameof(NuGetPMCExecuteCommandCount);
        public const string NuGetPMUIExecuteCommandCount = nameof(NuGetPMUIExecuteCommandCount);
        public const string NuGetCommandUsed = nameof(NuGetCommandUsed);
        public const string InitPs1LoadPMUI = nameof(InitPs1LoadPMUI);
        public const string InitPs1LoadPMC = nameof(InitPs1LoadPMC);
        public const string InitPs1LoadedFromPMCFirst = nameof(InitPs1LoadedFromPMCFirst);
        public const string LoadedFromPMUI = nameof(LoadedFromPMUI);
        public const string FirstTimeLoadedFromPMUI = nameof(FirstTimeLoadedFromPMUI);
        public const string LoadedFromPMC = nameof(LoadedFromPMC);
        public const string FirstTimeLoadedFromPMC = nameof(FirstTimeLoadedFromPMC);
        public const string SolutionLoaded = nameof(SolutionLoaded);
        public const string PowerShellExecuteCommand = nameof(PowerShellExecuteCommand);
        public const string NuGetPowerShellLoaded = nameof(NuGetPowerShellLoaded);

        // PMC UI Console Container telemetry consts
        public const string PackageManagerConsoleWindowsLoad = nameof(PackageManagerConsoleWindowsLoad);
        public const string NuGetPMCWindowLoadCount = nameof(NuGetPMCWindowLoadCount);
        public const string ReOpenAtStart = nameof(ReOpenAtStart);

        // Const name for emitting when VS solution close or VS instance close.
        public const string Name = nameof(Name);
        public const string NuGetPowershellPrefix = "NuGetPowershellPrefix."; // Using prefix prevent accidental same name property collission from different type telemetry.
        public const string NuGetVSSolutionClose = nameof(NuGetVSSolutionClose);
        public const string NuGetVSInstanceClose = nameof(NuGetVSInstanceClose);
        public const string SolutionCount = nameof(SolutionCount);
        public const string PMCPowerShellLoadedSolutionCount = nameof(PMCPowerShellLoadedSolutionCount);
        public const string PMUIPowerShellLoadedSolutionCount = nameof(PMUIPowerShellLoadedSolutionCount);

        private int _solutionCount;
        // _vsSolutionData hold telemetry data for current VS solution session.
        private NuGetPowershellVSSolutionCloseEvent _vsSolutionData;
        // _vsInstanceData hold telemetry for current VS instance session.
        private readonly NuGetPowershellVSInstanceCloseEvent _vsInstanceData;
        private TelemetryEvent _powerShellLoadEvent;
        private object _lock = new object();

        public NuGetPowerShellUsageCollector()
        {
            _vsSolutionData = new NuGetPowershellVSSolutionCloseEvent();
            _vsInstanceData = new NuGetPowershellVSInstanceCloseEvent();

            NuGetPowerShellUsage.PowerShellLoadEvent += NuGetPowerShellUsage_PMCLoadEventHandler;
            NuGetPowerShellUsage.PowerShellCommandExecuteEvent += NuGetPowerShellUsage_PowerShellCommandExecuteEvent;
            NuGetPowerShellUsage.InitPs1LoadEvent += NuGetPowerShellUsage_InitPs1LoadEvent;
            NuGetPowerShellUsage.PMCWindowsEvent += NuGetPowerShellUsage_PMCWindowsEventHandler;
            NuGetPowerShellUsage.SolutionOpenEvent += NuGetPowerShellUsage_SolutionOpenHandler;
            NuGetPowerShellUsage.SolutionCloseEvent += NuGetPowerShellUsage_SolutionCloseHandler;
            NuGetPowerShellUsage.VSInstanceCloseEvent += NuGetPowerShellUsage_VSInstanseCloseHandler;
        }

        private void NuGetPowerShellUsage_PMCLoadEventHandler(bool isPMC)
        {
            AddPowerShellLoadedData(isPMC, _vsSolutionData);
        }

        internal void AddPowerShellLoadedData(bool isPMC, NuGetPowershellVSSolutionCloseEvent vsSolutionData)
        {
            lock (_lock)
            {
                // PowerShellHost loaded first time, let's emit this to find out later how many VS instance crash after loading powershell.
                if (!vsSolutionData._loadedFromPMC && !vsSolutionData._loadedFromPMUI)
                {
                    var telemetryEvent = new TelemetryEvent(NuGetPowerShellLoaded, new Dictionary<string, object>
                        {
                            { NuGetPowershellPrefix + LoadedFromPMC, isPMC }
                        });

                    // Telemetry service is not ready then delay for while.
                    if (TelemetryActivity.NuGetTelemetryService == null)
                    {
                        _powerShellLoadEvent = telemetryEvent;
                    }
                    else
                    {
                        TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
                    }
                }

                if (isPMC)
                {
                    if (!vsSolutionData._loadedFromPMC)
                    {
                        vsSolutionData._firstTimeLoadedFromPMC = true;
                    }

                    vsSolutionData._loadedFromPMC = true;
                }
                else
                {
                    if (!vsSolutionData._loadedFromPMUI)
                    {
                        vsSolutionData._loadedFromPMUI = true;
                    }

                    vsSolutionData._loadedFromPMUI = true;
                }
            }
        }

        private void NuGetPowerShellUsage_PowerShellCommandExecuteEvent(bool isPMC, string commandStr)
        {
            AddPowerShellCommandExecuteData(isPMC, commandStr, _vsSolutionData);
        }

        internal void AddPowerShellCommandExecuteData(bool isPMC, string commandStr, NuGetPowershellVSSolutionCloseEvent vsSolutionData)
        {
            lock (_lock)
            {
                if (isPMC)
                {
                    {
                        // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                        // For PMC all installation done in one pass so no double counting.
                        vsSolutionData._nuGetPMCExecuteCommandCount++;
                    }
                }
                else
                {
                    // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                    // This one is called for both init.ps1 and install.ps1 seperately.
                    // For MSBuildNuGetProject projects install.ps1 can even further increase duplicate counting: See MSBuildNuGetProject.cs#L377 - L396
                    // Also this concern valid for dependent packages with *.ps1 files.
                    vsSolutionData._nuGetPMUIExecuteCommandCount++;
                }

                // Check and add to telemetery if a command is NugetCommand like 'install-package' etc. 
                if (vsSolutionData._nuGetCommandUsed)
                {
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
                            // Nuget Command executed
                            vsSolutionData._nuGetCommandUsed = true;
                            break;
                    }
                }
            }
        }

        private void NuGetPowerShellUsage_InitPs1LoadEvent(bool isPMC)
        {
            AddInitPs1LoadData(isPMC, _vsSolutionData);
        }

        internal void AddInitPs1LoadData(bool isPMC, NuGetPowershellVSSolutionCloseEvent vsSolutionData)
        {
            lock (_lock)
            {
                // Test bit flag for if init.ps1 already loaded from PMC or PMUI
                if (isPMC && (!vsSolutionData._firstTimeLoadedFromPMC && !vsSolutionData._firstTimeLoadedFromPMUI))
                {
                    // if not then set initialization origin bit
                    vsSolutionData._initPs1LoadedFromPMCFirst = true;
                }

                if (isPMC)
                {
                    vsSolutionData._initPs1LoadPMC = true;
                    vsSolutionData._loadedFromPMC = true;
                }
                else
                {
                    vsSolutionData._initPs1LoadPMUI = true;
                    vsSolutionData._loadedFromPMUI = true;
                }
            }
        }

        private void NuGetPowerShellUsage_PMCWindowsEventHandler(bool isLoad)
        {
            AddPMCWindowsEventData(isLoad);
        }

        internal void AddPMCWindowsEventData(bool isLoad)
        {
            lock (_lock)
            {
                if (isLoad)
                {
                    _vsSolutionData._nuGetPMCWindowLoadCount++;
                }

                // Shutdown call happen before Unload event for PMCWindow so VSInstanceClose event going to have current up to date status.
                _vsInstanceData._reOpenAtStart = isLoad;
            }
        }

        private void NuGetPowerShellUsage_SolutionOpenHandler()
        {
            lock (_lock)
            {
                // Emit previously not sent telemetry due to Telemetry service was not available yet.
                if (_powerShellLoadEvent != null)
                {
                    TelemetryActivity.EmitTelemetryEvent(_powerShellLoadEvent);
                    _powerShellLoadEvent = null;
                }

                // Edge case: PMC used without solution load
                if (!_vsSolutionData._solutionLoaded && _vsSolutionData._nuGetPMCExecuteCommandCount > 0)
                {
                    // PMC used before any solution is loaded, let's emit what we have for nugetvsinstanceclose event aggregation before loading a solution.
                    TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                    IncrementUpdateVSInstanceData();
                }

                ClearSolutionData();
            }
        }

        private void NuGetPowerShellUsage_SolutionCloseHandler()
        {
            lock (_lock)
            {
                _vsInstanceData._solutionCount = ++_solutionCount;
                _vsSolutionData._solutionLoaded = true;

                // Emit solution telemetry
                TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                IncrementUpdateVSInstanceData();
                ClearSolutionData();
            }
        }

        private void NuGetPowerShellUsage_VSInstanseCloseHandler()
        {
            lock (_lock)
            {
                if (_powerShellLoadEvent != null)
                {
                    TelemetryActivity.EmitTelemetryEvent(_powerShellLoadEvent);
                    _powerShellLoadEvent = null;
                }

                // Edge case: PMC used without solution load
                if (!_vsSolutionData._solutionLoaded && _vsSolutionData._nuGetPMCExecuteCommandCount > 0)
                {
                    // PMC used before any solution is loaded, let's emit what we have for nugetvsinstanceclose event aggregation before loading a solution.
                    TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                    IncrementUpdateVSInstanceData();
                }

                // Emit VS Instance telemetry
                TelemetryActivity.EmitTelemetryEvent(_vsInstanceData.ToTelemetryEvent());
            }
        }

        //If open new solution then need to clear previous solution events. But powershell remain loaded in memory.
        private void ClearSolutionData()
        {
            bool pmcPowershellLoad = _vsSolutionData._loadedFromPMC;
            bool pmuiPowershellLoad = _vsSolutionData._loadedFromPMUI;

            _vsSolutionData = new NuGetPowershellVSSolutionCloseEvent();

            _vsSolutionData._loadedFromPMC = pmcPowershellLoad;
            _vsSolutionData._loadedFromPMUI = pmuiPowershellLoad;
        }

        private void IncrementUpdateVSInstanceData()
        {
            _vsInstanceData._nugetPMCExecuteCommandCount += _vsSolutionData._nuGetPMCExecuteCommandCount;
            _vsInstanceData._nugetPMCWindowLoadCount += _vsSolutionData._nuGetPMCWindowLoadCount;
            _vsInstanceData._nugetPMUIExecuteCommandCount += _vsSolutionData._nuGetPMUIExecuteCommandCount;

            if (_vsSolutionData._loadedFromPMC)
            {
                _vsInstanceData._pmcLoadedSolutionCount++;
            }

            if (_vsSolutionData._loadedFromPMUI)
            {
                _vsInstanceData._pmuiLoadedSolutionCount++;
            }
        }

        public void Dispose()
        {
            NuGetPowerShellUsage.PowerShellLoadEvent -= NuGetPowerShellUsage_PMCLoadEventHandler;
            NuGetPowerShellUsage.PowerShellCommandExecuteEvent -= NuGetPowerShellUsage_PowerShellCommandExecuteEvent;
            NuGetPowerShellUsage.InitPs1LoadEvent -= NuGetPowerShellUsage_InitPs1LoadEvent;
            NuGetPowerShellUsage.PMCWindowsEvent -= NuGetPowerShellUsage_PMCWindowsEventHandler;
            NuGetPowerShellUsage.SolutionOpenEvent -= NuGetPowerShellUsage_SolutionOpenHandler;
            NuGetPowerShellUsage.SolutionCloseEvent -= NuGetPowerShellUsage_SolutionCloseHandler;
            NuGetPowerShellUsage.VSInstanceCloseEvent -= NuGetPowerShellUsage_VSInstanseCloseHandler;
        }

        internal class NuGetPowershellVSSolutionCloseEvent
        {
            internal bool _firstTimeLoadedFromPMC;
            internal bool _firstTimeLoadedFromPMUI;
            internal bool _initPs1LoadedFromPMCFirst;
            internal bool _initPs1LoadPMC;
            internal bool _initPs1LoadPMUI;
            internal bool _loadedFromPMC;
            internal bool _loadedFromPMUI;
            internal bool _nuGetCommandUsed;
            internal int _nuGetPMCExecuteCommandCount;
            internal int _nuGetPMCWindowLoadCount;
            internal int _nuGetPMUIExecuteCommandCount;
            internal bool _solutionLoaded;

            internal NuGetPowershellVSSolutionCloseEvent()
            {
                _firstTimeLoadedFromPMC = false;
                _firstTimeLoadedFromPMUI = false;
                _initPs1LoadedFromPMCFirst = false;
                _initPs1LoadPMC = false;
                _initPs1LoadPMUI = false;
                _loadedFromPMC = false;
                _loadedFromPMUI = false;
                _nuGetCommandUsed = false;
                _nuGetPMCExecuteCommandCount = 0;
                _nuGetPMCWindowLoadCount = 0;
                _nuGetPMUIExecuteCommandCount = 0;
                _solutionLoaded = false;

            }

            internal TelemetryEvent ToTelemetryEvent()
            {
                var telemetry = new TelemetryEvent(NuGetVSSolutionClose,
                    new Dictionary<string, object>()
                    {
                        { NuGetPowershellPrefix + FirstTimeLoadedFromPMC , _firstTimeLoadedFromPMC },
                        { NuGetPowershellPrefix + FirstTimeLoadedFromPMUI , _firstTimeLoadedFromPMUI },
                        { NuGetPowershellPrefix + InitPs1LoadedFromPMCFirst , _initPs1LoadedFromPMCFirst },
                        { NuGetPowershellPrefix + InitPs1LoadPMC , _initPs1LoadPMC },
                        { NuGetPowershellPrefix + InitPs1LoadPMUI , _initPs1LoadPMUI },
                        { NuGetPowershellPrefix + LoadedFromPMC , _loadedFromPMC },
                        { NuGetPowershellPrefix + LoadedFromPMUI , _loadedFromPMUI },
                        { NuGetPowershellPrefix + NuGetCommandUsed , _nuGetCommandUsed },
                        { NuGetPowershellPrefix + NuGetPMCExecuteCommandCount , _nuGetPMCExecuteCommandCount },
                        { NuGetPowershellPrefix + NuGetPMCWindowLoadCount , _nuGetPMCWindowLoadCount },
                        { NuGetPowershellPrefix + NuGetPMUIExecuteCommandCount , _nuGetPMUIExecuteCommandCount },
                        { NuGetPowershellPrefix + SolutionLoaded , _solutionLoaded }
                    });

                return telemetry;
            }
        }

        internal class NuGetPowershellVSInstanceCloseEvent
        {
            internal int _nugetPMCExecuteCommandCount;
            internal int _nugetPMCWindowLoadCount;
            internal int _nugetPMUIExecuteCommandCount;
            internal int _pmcLoadedSolutionCount;
            internal int _pmuiLoadedSolutionCount;
            internal bool _reOpenAtStart;
            internal int _solutionCount;

            internal NuGetPowershellVSInstanceCloseEvent()
            {
                _nugetPMCExecuteCommandCount = 0;
                _nugetPMCWindowLoadCount = 0;
                _nugetPMUIExecuteCommandCount = 0;
                _pmcLoadedSolutionCount = 0;
                _pmuiLoadedSolutionCount = 0;
                _reOpenAtStart = false;
                _solutionCount = 0;
            }

            internal TelemetryEvent ToTelemetryEvent()
            {
                var telemetry = new TelemetryEvent(NuGetVSInstanceClose,
                    new Dictionary<string, object>()
                    {
                        { NuGetPowershellPrefix + NuGetPMCExecuteCommandCount , _nugetPMCExecuteCommandCount },
                        { NuGetPowershellPrefix + NuGetPMCWindowLoadCount , _nugetPMCWindowLoadCount },
                        { NuGetPowershellPrefix + NuGetPMUIExecuteCommandCount , _nugetPMUIExecuteCommandCount },
                        { NuGetPowershellPrefix + PMCPowerShellLoadedSolutionCount , _pmcLoadedSolutionCount },
                        { NuGetPowershellPrefix + PMUIPowerShellLoadedSolutionCount , _pmuiLoadedSolutionCount },
                        { NuGetPowershellPrefix + ReOpenAtStart , _reOpenAtStart },
                        { NuGetPowershellPrefix + SolutionCount , _solutionCount },
                    });

                return telemetry;
            }
        }
    }
}
