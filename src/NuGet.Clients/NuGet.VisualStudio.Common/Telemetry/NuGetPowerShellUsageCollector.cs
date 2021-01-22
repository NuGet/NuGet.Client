// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry.PowerShell;

namespace NuGet.VisualStudio.Telemetry
{
    public sealed class NuGetPowerShellUsageCollector : IDisposable
    {
        // PMC, PMUI powershell telemetry consts
        public const string PmcExecuteCommandCount = nameof(PmcExecuteCommandCount);
        public const string PmuiExecuteCommandCount = nameof(PmuiExecuteCommandCount);
        public const string NuGetCommandUsed = nameof(NuGetCommandUsed);
        public const string InitPs1LoadPMUI = nameof(InitPs1LoadPMUI);
        public const string InitPs1LoadPMC = nameof(InitPs1LoadPMC);
        public const string InitPs1LoadedFromPMCFirst = nameof(InitPs1LoadedFromPMCFirst);
        public const string LoadedFromPMUI = nameof(LoadedFromPMUI);
        public const string FirstTimeLoadedFromPMUI = nameof(FirstTimeLoadedFromPMUI);
        public const string LoadedFromPMC = nameof(LoadedFromPMC);
        public const string FirstTimeLoadedFromPMC = nameof(FirstTimeLoadedFromPMC);
        public const string SolutionLoaded = nameof(SolutionLoaded);
        public const string Trigger = nameof(Trigger);
        public const string Pmc = nameof(Pmc);
        public const string Pmui = nameof(Pmui);

        // PMC UI Console Container telemetry consts
        public const string PmcWindowLoadCount = nameof(PmcWindowLoadCount);
        public const string ReOpenAtStart = nameof(ReOpenAtStart);

        // Const name for emitting when VS solution close or VS instance close.
        public const string SolutionClose = nameof(SolutionClose);
        public const string InstanceClose = nameof(InstanceClose);
        public const string PowerShellHost = "PowerShellHost.";
        public const string SolutionCount = nameof(SolutionCount);
        public const string PmcPowerShellLoadedSolutionCount = nameof(PmcPowerShellLoadedSolutionCount);
        public const string PmuiPowerShellLoadedSolutionCount = nameof(PmuiPowerShellLoadedSolutionCount);
        public const string PowerShellLoaded = nameof(PowerShellLoaded);

        private int _solutionCount;
        private SolutionData _vsSolutionData;
        private readonly InstanceData _vsInstanceData;
        private object _lock = new object();

        public NuGetPowerShellUsageCollector()
        {
            _vsSolutionData = new SolutionData();
            _vsInstanceData = new InstanceData();

            NuGetPowerShellUsage.PowerShellLoadEvent += NuGetPowerShellUsage_PMCLoadEventHandler;
            NuGetPowerShellUsage.PowerShellCommandExecuteEvent += NuGetPowerShellUsage_PowerShellCommandExecuteEventHandler;
            NuGetPowerShellUsage.NuGetCmdletExecutedEvent += NuGetPowerShellUsage_NuGetCmdletExecutedEventHandler;
            NuGetPowerShellUsage.InitPs1LoadEvent += NuGetPowerShellUsage_InitPs1LoadEventHandler;
            NuGetPowerShellUsage.PMCWindowsEvent += NuGetPowerShellUsage_PMCWindowsEventHandler;
            NuGetPowerShellUsage.SolutionOpenEvent += NuGetPowerShellUsage_SolutionOpenHandler;
            NuGetPowerShellUsage.SolutionCloseEvent += NuGetPowerShellUsage_SolutionCloseHandler;
            NuGetPowerShellUsage.VSInstanceCloseEvent += NuGetPowerShellUsage_VSInstanseCloseHandler;
        }

        private void NuGetPowerShellUsage_PMCLoadEventHandler(bool isPMC)
        {
            AddPowerShellLoadedData(isPMC, _vsSolutionData);
        }

        internal void AddPowerShellLoadedData(bool isPMC, SolutionData vsSolutionData)
        {
            lock (_lock)
            {
                if (isPMC)
                {
                    if (!vsSolutionData.LoadedFromPMC)
                    {
                        vsSolutionData.FirstTimeLoadedFromPMC = true;
                        _vsInstanceData.PMCLoadedSolutionCount++;
                    }

                    vsSolutionData.LoadedFromPMC = true;
                }
                else
                {
                    if (!vsSolutionData.LoadedFromPMUI)
                    {
                        vsSolutionData.FirstTimeLoadedFromPMUI = true;
                        _vsInstanceData.PMUILoadedSolutionCount++;
                    }

                    vsSolutionData.LoadedFromPMUI = true;
                }
            }
        }

        private void NuGetPowerShellUsage_PowerShellCommandExecuteEventHandler(bool isPMC)
        {
            AddPowerShellCommandExecuteData(isPMC, _vsSolutionData);
        }

        internal void AddPowerShellCommandExecuteData(bool isPMC, SolutionData vsSolutionData)
        {
            lock (_lock)
            {
                // Please note: Direct PMC and PMUI don't share same code path for installing packages with *.ps1 files
                if (isPMC)
                {
                    // For PMC all installation done in one pass so no double counting.
                    vsSolutionData.PMCExecuteCommandCount++;
                    _vsInstanceData.PMCExecuteCommandCount++;
                }
                else
                {
                    // This one is called for both init.ps1 and install.ps1 seperately.
                    // install.ps1 running inside MSBuildNuGetProject.cs (InstallPackageAsync method) may result in duplicate counting.
                    // Also this concern valid for dependent packages (of installing package) with *.ps1 files.
                    vsSolutionData.PMUIExecuteCommandCount++;
                    _vsInstanceData.PMUIExecuteCommandCount++;
                }
            }
        }

        private void NuGetPowerShellUsage_NuGetCmdletExecutedEventHandler()
        {
            AddNuGetCmdletExecutedData(_vsSolutionData);
        }

        internal void AddNuGetCmdletExecutedData(SolutionData vsSolutionData)
        {
            lock (_lock)
            {
                vsSolutionData.NuGetCommandUsed = true;
            }
        }

        private void NuGetPowerShellUsage_InitPs1LoadEventHandler(bool isPMC)
        {
            AddInitPs1LoadData(isPMC, _vsSolutionData);
        }

        internal void AddInitPs1LoadData(bool isPMC, SolutionData vsSolutionData)
        {
            lock (_lock)
            {
                if (isPMC && (!vsSolutionData.FirstTimeLoadedFromPMC && !vsSolutionData.FirstTimeLoadedFromPMUI))
                {
                    vsSolutionData.InitPs1LoadedFromPMCFirst = true;
                }

                if (isPMC)
                {
                    vsSolutionData.InitPs1LoadPMC = true;

                    if (!vsSolutionData.LoadedFromPMC)
                    {
                        vsSolutionData.FirstTimeLoadedFromPMC = true;
                        _vsInstanceData.PMCLoadedSolutionCount++;
                    }

                    vsSolutionData.LoadedFromPMC = true;
                }
                else
                {
                    vsSolutionData.InitPs1LoadPMUI = true;

                    if (!vsSolutionData.LoadedFromPMUI)
                    {
                        vsSolutionData.FirstTimeLoadedFromPMUI = true;
                        _vsInstanceData.PMUILoadedSolutionCount++;
                    }

                    vsSolutionData.LoadedFromPMUI = true;
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
                    _vsSolutionData.PMCWindowLoadCount++;
                    _vsInstanceData.PMCWindowLoadCount++;
                }

                // Shutdown call happen before Unload event for PMCWindow so VSInstanceClose event going to have current up to date status.
                _vsInstanceData.ReOpenAtStart = isLoad;
            }
        }

        private void NuGetPowerShellUsage_SolutionOpenHandler()
        {
            lock (_lock)
            {
                // Edge case: PMC used without solution load
                if (!_vsSolutionData.SolutionLoaded && _vsSolutionData.PMCExecuteCommandCount > 0)
                {
                    // PMC used before any solution is loaded, let's emit what we have for nugetvsinstanceclose event.
                    TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                    ClearSolutionData();
                }

                if (_vsSolutionData.LoadedFromPMC)
                {
                    _vsInstanceData.PMCLoadedSolutionCount++;
                }

                if (_vsSolutionData.LoadedFromPMUI)
                {
                    _vsInstanceData.PMUILoadedSolutionCount++;
                }

                _vsInstanceData.SolutionCount = ++_solutionCount;
                _vsSolutionData.SolutionLoaded = true;
            }
        }

        private void NuGetPowerShellUsage_SolutionCloseHandler()
        {
            lock (_lock)
            {
                TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                ClearSolutionData();
            }
        }

        private void NuGetPowerShellUsage_VSInstanseCloseHandler()
        {
            lock (_lock)
            {
                // Edge case: PMC used without solution load
                if (!_vsSolutionData.SolutionLoaded && _vsSolutionData.PMCExecuteCommandCount > 0)
                {
                    // PMC used before any solution is loaded, let's emit what we have for nugetvsinstanceclose event.
                    TelemetryActivity.EmitTelemetryEvent(_vsSolutionData.ToTelemetryEvent());
                }

                // Emit VS Instance telemetry
                TelemetryActivity.EmitTelemetryEvent(_vsInstanceData.ToTelemetryEvent());
            }
        }

        // If open new solution then need to clear previous solution events. But powershell remain loaded in memory.
        private void ClearSolutionData()
        {
            bool pmcPowershellLoad = _vsSolutionData.LoadedFromPMC;
            bool pmuiPowershellLoad = _vsSolutionData.LoadedFromPMUI;

            _vsSolutionData = new SolutionData();

            _vsSolutionData.LoadedFromPMC = pmcPowershellLoad;
            _vsSolutionData.LoadedFromPMUI = pmuiPowershellLoad;
        }

        public void Dispose()
        {
            NuGetPowerShellUsage.PowerShellLoadEvent -= NuGetPowerShellUsage_PMCLoadEventHandler;
            NuGetPowerShellUsage.PowerShellCommandExecuteEvent -= NuGetPowerShellUsage_PowerShellCommandExecuteEventHandler;
            NuGetPowerShellUsage.NuGetCmdletExecutedEvent -= NuGetPowerShellUsage_NuGetCmdletExecutedEventHandler;
            NuGetPowerShellUsage.InitPs1LoadEvent -= NuGetPowerShellUsage_InitPs1LoadEventHandler;
            NuGetPowerShellUsage.PMCWindowsEvent -= NuGetPowerShellUsage_PMCWindowsEventHandler;
            NuGetPowerShellUsage.SolutionOpenEvent -= NuGetPowerShellUsage_SolutionOpenHandler;
            NuGetPowerShellUsage.SolutionCloseEvent -= NuGetPowerShellUsage_SolutionCloseHandler;
            NuGetPowerShellUsage.VSInstanceCloseEvent -= NuGetPowerShellUsage_VSInstanseCloseHandler;
        }

        internal class SolutionData
        {
            internal bool FirstTimeLoadedFromPMC { get; set; }
            internal bool FirstTimeLoadedFromPMUI { get; set; }
            internal bool InitPs1LoadedFromPMCFirst { get; set; }
            internal bool InitPs1LoadPMC { get; set; }
            internal bool InitPs1LoadPMUI { get; set; }
            internal bool LoadedFromPMC { get; set; }
            internal bool LoadedFromPMUI { get; set; }
            internal bool NuGetCommandUsed { get; set; }
            internal int PMCExecuteCommandCount { get; set; }
            internal int PMCWindowLoadCount { get; set; }
            internal int PMUIExecuteCommandCount { get; set; }
            internal bool SolutionLoaded { get; set; }

            internal SolutionData()
            {
                FirstTimeLoadedFromPMC = false;
                FirstTimeLoadedFromPMUI = false;
                InitPs1LoadedFromPMCFirst = false;
                InitPs1LoadPMC = false;
                InitPs1LoadPMUI = false;
                LoadedFromPMC = false;
                LoadedFromPMUI = false;
                NuGetCommandUsed = false;
                PMCExecuteCommandCount = 0;
                PMCWindowLoadCount = 0;
                PMUIExecuteCommandCount = 0;
                SolutionLoaded = false;

            }

            internal TelemetryEvent ToTelemetryEvent()
            {
                var telemetry = new TelemetryEvent(SolutionClose,
                    new Dictionary<string, object>()
                    {
                        { PowerShellHost + NuGetPowerShellUsageCollector.FirstTimeLoadedFromPMC , FirstTimeLoadedFromPMC },
                        { PowerShellHost + NuGetPowerShellUsageCollector.FirstTimeLoadedFromPMUI , FirstTimeLoadedFromPMUI },
                        { PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadedFromPMCFirst , InitPs1LoadedFromPMCFirst },
                        { PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadPMC , InitPs1LoadPMC },
                        { PowerShellHost + NuGetPowerShellUsageCollector.InitPs1LoadPMUI , InitPs1LoadPMUI },
                        { PowerShellHost + NuGetPowerShellUsageCollector.LoadedFromPMC , LoadedFromPMC },
                        { PowerShellHost + NuGetPowerShellUsageCollector.LoadedFromPMUI , LoadedFromPMUI },
                        { PowerShellHost + NuGetPowerShellUsageCollector.NuGetCommandUsed , NuGetCommandUsed },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmcExecuteCommandCount , PMCExecuteCommandCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmcWindowLoadCount , PMCWindowLoadCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmuiExecuteCommandCount , PMUIExecuteCommandCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.SolutionLoaded , SolutionLoaded }
                    });

                return telemetry;
            }
        }

        internal class InstanceData
        {
            internal int PMCExecuteCommandCount { get; set; }
            internal int PMCWindowLoadCount { get; set; }
            internal int PMUIExecuteCommandCount { get; set; }
            internal int PMCLoadedSolutionCount { get; set; }
            internal int PMUILoadedSolutionCount { get; set; }
            internal bool ReOpenAtStart { get; set; }
            internal int SolutionCount { get; set; }

            internal InstanceData()
            {
                PMCExecuteCommandCount = 0;
                PMCWindowLoadCount = 0;
                PMUIExecuteCommandCount = 0;
                PMCLoadedSolutionCount = 0;
                PMUILoadedSolutionCount = 0;
                ReOpenAtStart = false;
                SolutionCount = 0;
            }

            internal TelemetryEvent ToTelemetryEvent()
            {
                var telemetry = new TelemetryEvent(InstanceClose,
                    new Dictionary<string, object>()
                    {
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmcExecuteCommandCount , PMCExecuteCommandCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmcWindowLoadCount , PMCWindowLoadCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmuiExecuteCommandCount , PMUIExecuteCommandCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmcPowerShellLoadedSolutionCount , PMCLoadedSolutionCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.PmuiPowerShellLoadedSolutionCount , PMUILoadedSolutionCount },
                        { PowerShellHost + NuGetPowerShellUsageCollector.ReOpenAtStart , ReOpenAtStart },
                        { PowerShellHost + NuGetPowerShellUsageCollector.SolutionCount , SolutionCount },
                    });

                return telemetry;
            }
        }
    }
}
