// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry.Powershell;
using TelemetryConst = NuGet.VisualStudio.Telemetry.VSPowershellTelemetryConsts;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(VsInstancePowershellTelemetryEmitter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsInstancePowershellTelemetryEmitter
    {
        private int _solutionCount;
        //private Lazy<Dictionary<string, object>> _vsSolutionTelemetryEmitQueue;
        //private Lazy<Dictionary<string, object>> _vsInstanceTelemetryEmitQueue;
        private Lazy<ConcurrentBag<TelemetryEvent>> _vsSolutionTelemetryEmitQueue;
        private Lazy<ConcurrentBag<TelemetryEvent>> _vsInstanceTelemetryEmitQueue;
        private INuGetTelemetryCollector _nuGetTelemetryCollector;
        private Lazy<IReadOnlyList<TelemetryEvent>> _vsInstanceTelemetryEvents;

        [ImportingConstructor]
        internal VsInstancePowershellTelemetryEmitter(INuGetTelemetryCollector nuGetTelemetryCollector)
        {
            //_vsSolutionTelemetryEmitQueue = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());
            //_vsInstanceTelemetryEmitQueue = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());
            _vsSolutionTelemetryEmitQueue = new Lazy<ConcurrentBag<TelemetryEvent>>(() => new ConcurrentBag<TelemetryEvent>());
            _vsInstanceTelemetryEmitQueue = new Lazy<ConcurrentBag<TelemetryEvent>>(() => new ConcurrentBag<TelemetryEvent>());
            _nuGetTelemetryCollector = nuGetTelemetryCollector;
            _vsInstanceTelemetryEvents = new Lazy<IReadOnlyList<TelemetryEvent>>(() =>
            {
                return _nuGetTelemetryCollector?.GetVSIntanceTelemetryEvents();
            });
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
            catch (Exception)
            {
                // Currently do nothing.
            }
        }

        // Emit VS solution session telemetry when solution is closed.
        public void SolutionClosedEmit()
        {
            try
            {
                // Queue all different types of telemetries and do some processing prior to emit.
                VSSolutionPowershellTelemetryEmit();
                _nuGetTelemetryCollector.ClearSolutionTelemetryEvents();
            }
            catch (Exception)
            {
                // Currently do nothing.
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
            catch (Exception)
            {
                // Currently do nothing.
            }
        }

        private void EmitPMCUsedWithoutSolution()
        {
            // Edge case: PMC window can be opened without any solution at all, but sometimes TelemetryActivity.NuGetTelemetryService is not ready yet when PMC open.
            // In general we want to emit this telemetry right away, but not possible then emit later.
            var nuGetPowerShellLoadedEvent = _nuGetTelemetryCollector.GetSolutionTelemetryEvents().FirstOrDefault(e => e.Name == TelemetryConst.NuGetPowerShellLoaded);

            if (nuGetPowerShellLoadedEvent != null)
            {
                TelemetryActivity.EmitTelemetryEvent(nuGetPowerShellLoadedEvent);
                //vsSolutionTelemetryEvents.Remove(nuGetPowerShellLoadedEvent);
            }

            // If there is not emitted PowerShellExecuteCommand telemetry.
            if (_nuGetTelemetryCollector.GetSolutionTelemetryEvents().Any(e => e is NuGetPowershellVSSolutionCloseEvent))
            {
                SolutionClosedEmit();
            }
        }

        private void VSSolutionPowershellTelemetryEmit()
        {
            NuGetPowershellVSSolutionCloseEvent vsSolutionPowershellTelemetry = _nuGetTelemetryCollector.GetSolutionTelemetryEvents().FirstOrDefault(e => e is NuGetPowershellVSSolutionCloseEvent) as NuGetPowershellVSSolutionCloseEvent;

            // If powershell(PMC/PMUI) is not loaded at all then we need to create default telemetry event which will be emitted.
            if (vsSolutionPowershellTelemetry == null)
            {
                vsSolutionPowershellTelemetry = new NuGetPowershellVSSolutionCloseEvent();
            }
            else
            {
                // PMC opened, but no command executed nor any solution was loaded. Rather than sending separate nugetvssolutionclose telemetry with no data just ignore.
                if (!(bool)vsSolutionPowershellTelemetry[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.SolutionLoaded] && (int)vsSolutionPowershellTelemetry[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCExecuteCommandCount] == 0)
                {
                    return;
                }

                vsSolutionPowershellTelemetry[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCWindowLoadCount] = _nuGetTelemetryCollector.GetSolutionTelemetryEvents().Where(e => e[TelemetryConst.NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[TelemetryConst.NuGetPMCWindowLoadCount]);
            }

            TelemetryActivity.EmitTelemetryEvent(vsSolutionPowershellTelemetry);
        }

        private void VSInstancePowershellTelemetryEmit()
        {
            NuGetPowershellVSInstanceCloseEvent nuGetPowershellVSInstanceCloseEvent = new NuGetPowershellVSInstanceCloseEvent(
                nugetpmcexecutecommandcount : _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCExecuteCommandCount] is int).Sum(e => (int)e[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMCExecuteCommandCount]),
                nugetpmcwindowloadcount :  _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[TelemetryConst.NuGetPMCWindowLoadCount]),
                nugetpmuiexecutecommandcount: _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMUIExecuteCommandCount] is int).Sum(e => (int)e[TelemetryConst.NuGetPowershellPrefix + TelemetryConst.NuGetPMUIExecuteCommandCount]),
                pmcpowershellloadedsolutioncount: _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.SolutionLoaded] is bool && (bool)e[TelemetryConst.SolutionLoaded] && e[TelemetryConst.LoadedFromPMC] is bool).Count(e => (bool)e[TelemetryConst.LoadedFromPMC] == true),
                pmuipowershellloadedsolutioncount: _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.LoadedFromPMUI] is bool).Count(e => (bool)e[TelemetryConst.LoadedFromPMUI] == true),
                reopenatstart: _vsInstanceTelemetryEvents.Value.Where(e => e[TelemetryConst.ReOpenAtStart] != null).Any(),
                solutioncount: _solutionCount
                );

            TelemetryActivity.EmitTelemetryEvent(nuGetPowershellVSInstanceCloseEvent);
        }
    }
}
