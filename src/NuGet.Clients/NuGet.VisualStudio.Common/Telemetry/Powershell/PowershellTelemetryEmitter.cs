// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;
using TelemetryConst = NuGet.VisualStudio.Telemetry.PowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    [Export(typeof(PowershellTelemetryEmitter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class PowershellTelemetryEmitter
    {
        private int _solutionCount;
        private bool _powershellLoadEventEmitted = false;
        private INuGetTelemetryCollector _nuGetTelemetryCollector;
        private Func<List<Dictionary<string, object>>> _vsSolutionTelemetryEvents;
        private Func<List<Dictionary<string, object>>> _vsInstanceTelemetryEvents;
        private readonly INuGetTelemetryProvider _telemetryProvider;

        [ImportingConstructor]
        internal PowershellTelemetryEmitter(INuGetTelemetryCollector nuGetTelemetryCollector, INuGetTelemetryProvider telemetryProvider)
        {
            _nuGetTelemetryCollector = nuGetTelemetryCollector;
            _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));
            _vsSolutionTelemetryEvents = () => _nuGetTelemetryCollector?.GetVSSolutionTelemetryEvents().ToList();
            _vsInstanceTelemetryEvents = () => _nuGetTelemetryCollector?.GetVSIntanceTelemetryEvents().ToList();
        }

        public void SolutionOpenedTelemetryEmit()
        {
            try
            {
                // Handle edge cases.
                EmitPMCUsedWithoutSolution();
                _nuGetTelemetryCollector.ClearSolutionTelemetryEvents();
                _solutionCount++;
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(PowershellTelemetryEmitter).FullName);
            }
        }

        // Emit VS solution session telemetry when solution is closed.
        public void SolutionClosedTelemetryEmit()
        {
            try
            {
                // Queue all different types of telemetries and do some processing prior to emit.
                VSSolutionPowershellTelemetryEmit();
                _nuGetTelemetryCollector.ClearSolutionTelemetryEvents();
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(PowershellTelemetryEmitter).FullName);
            }
        }

        // Emit VS solution session telemetry when VS instance is closed.
        public void VSInstanceClosedTelemetryEmit()
        {
            try
            {
                // Handle edge cases.
                EmitPMCUsedWithoutSolution();
                VSInstancePowershellTelemetryEmit();
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(PowershellTelemetryEmitter).FullName);
            }
        }

        private void EmitPMCUsedWithoutSolution()
        {
            // Edge case: PMC window can be opened without any solution at all, but sometimes TelemetryActivity.NuGetTelemetryService is not ready yet when PMC open.
            // In general we want to emit this telemetry right away, but not possible then emit later.

            if (!_powershellLoadEventEmitted)
            {
                Dictionary<string, object> nuGetPowerShellLoadedEvent = _vsSolutionTelemetryEvents().FirstOrDefault(e => (string)e[TelemetryConst.Name] == TelemetryConst.NuGetPowerShellLoaded);

                if (nuGetPowerShellLoadedEvent != null)
                {
                    TelemetryActivity.EmitTelemetryEvent(
                        new NuGetPowershellLoadedEvent(
                            loadedfrompmc: (bool)nuGetPowerShellLoadedEvent[TelemetryConst.LoadedFromPMC])
                        );
                    _powershellLoadEventEmitted = true;
                }
            }

            // If there is not emitted PowerShellExecuteCommand telemetry.
            if (_vsSolutionTelemetryEvents().Where(e => (string)e[TelemetryConst.Name] == TelemetryConst.NuGetVSSolutionClose).Any())
            {
                SolutionClosedTelemetryEmit();
            }
        }

        private void VSSolutionPowershellTelemetryEmit()
        {
            Dictionary<string, object> vsSolutionPowershellTelemetry = _vsSolutionTelemetryEvents().Where(e => (string)e[TelemetryConst.Name] == TelemetryConst.NuGetVSSolutionClose).FirstOrDefault();

            // If powershell(PMC/PMUI) is not loaded at all then we need to create default telemetry event which will be emitted.
            var nugetVSSolutionCloseEvent = new NuGetPowershellVSSolutionCloseEvent();

            if (vsSolutionPowershellTelemetry != null)
            {
                // PMC opened, but no command executed nor any solution was loaded. Rather than sending separate nugetvssolutionclose telemetry with no data just ignore.
                if (!(bool)vsSolutionPowershellTelemetry[TelemetryConst.SolutionLoaded] && (int)vsSolutionPowershellTelemetry[TelemetryConst.NuGetPMCExecuteCommandCount] == 0)
                {
                    return;
                }

                nugetVSSolutionCloseEvent = new NuGetPowershellVSSolutionCloseEvent(
                    FirstTimeLoadedFromPMC: (bool)vsSolutionPowershellTelemetry[TelemetryConst.FirstTimeLoadedFromPMC],
                    FirstTimeLoadedFromPMUI: (bool)vsSolutionPowershellTelemetry[TelemetryConst.FirstTimeLoadedFromPMUI],
                    InitPs1LoadedFromPMCFirst: (bool)vsSolutionPowershellTelemetry[TelemetryConst.InitPs1LoadedFromPMCFirst],
                    InitPs1LoadPMC: (bool)vsSolutionPowershellTelemetry[TelemetryConst.InitPs1LoadPMC],
                    InitPs1LoadPMUI: (bool)vsSolutionPowershellTelemetry[TelemetryConst.InitPs1LoadPMUI],
                    LoadedFromPMC: (bool)vsSolutionPowershellTelemetry[TelemetryConst.LoadedFromPMC],
                    LoadedFromPMUI: (bool)vsSolutionPowershellTelemetry[TelemetryConst.LoadedFromPMUI],
                    NuGetCommandUsed: (bool)vsSolutionPowershellTelemetry[TelemetryConst.NuGetCommandUsed],
                    NuGetPMCExecuteCommandCount: (int)vsSolutionPowershellTelemetry[TelemetryConst.NuGetPMCExecuteCommandCount],
                    NuGetPMCWindowLoadCount: _vsSolutionTelemetryEvents().Where(e => e[TelemetryConst.NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[TelemetryConst.NuGetPMCWindowLoadCount]),
                    NuGetPMUIExecuteCommandCount: (int)vsSolutionPowershellTelemetry[TelemetryConst.NuGetPMUIExecuteCommandCount],
                    SolutionLoaded: (bool)vsSolutionPowershellTelemetry[TelemetryConst.SolutionLoaded]
                 );
            }

            TelemetryActivity.EmitTelemetryEvent(nugetVSSolutionCloseEvent);
        }

        private void VSInstancePowershellTelemetryEmit()
        {
            List<Dictionary<string, object>> nuGetVSSolutionCloseEvents = _vsInstanceTelemetryEvents().Where(e => (string)e[TelemetryConst.Name] == TelemetryConst.NuGetVSSolutionClose).ToList();
            List<Dictionary<string, object>> packageManagerConsoleWindowsLoadEvents = _vsInstanceTelemetryEvents().Where(e => (string)e[TelemetryConst.Name] == TelemetryConst.PackageManagerConsoleWindowsLoad).ToList();

            NuGetPowershellVSInstanceCloseEvent nuGetPowershellVSInstanceCloseEvent = new NuGetPowershellVSInstanceCloseEvent(
                nugetpmcexecutecommandcount: nuGetVSSolutionCloseEvents.Sum(e => (int)e[TelemetryConst.NuGetPMCExecuteCommandCount]),
                nugetpmcwindowloadcount: packageManagerConsoleWindowsLoadEvents.Sum(e => (int)e[TelemetryConst.NuGetPMCWindowLoadCount]),
                nugetpmuiexecutecommandcount: nuGetVSSolutionCloseEvents.Sum(e => (int)e[TelemetryConst.NuGetPMUIExecuteCommandCount]),
                pmcpowershellloadedsolutioncount: nuGetVSSolutionCloseEvents.Count(e => (bool)e[TelemetryConst.LoadedFromPMC] == true),
                pmuipowershellloadedsolutioncount: nuGetVSSolutionCloseEvents.Count(e => (bool)e[TelemetryConst.LoadedFromPMUI] == true),
                reopenatstart: packageManagerConsoleWindowsLoadEvents.Any(e => e[TelemetryConst.ReOpenAtStart] != null),
                solutioncount: _solutionCount
                );

            TelemetryActivity.EmitTelemetryEvent(nuGetPowershellVSInstanceCloseEvent);
        }
    }
}
