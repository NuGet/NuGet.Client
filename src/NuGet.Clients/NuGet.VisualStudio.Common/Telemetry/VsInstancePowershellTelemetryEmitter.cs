// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(VsInstancePowershellTelemetryEmitter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsInstancePowershellTelemetryEmitter : VsInstanceTelemetryConsts
    {
        private int _solutionCount;
        private Lazy<Dictionary<string, object>> _vsSolutionTelemetryEmitQueue;
        private Lazy<Dictionary<string, object>> _vsInstanceTelemetryEmitQueue;
        private INuGetTelemetryCollector _nuGetTelemetryCollector;
        private Lazy<IReadOnlyList<TelemetryEvent>> _vsInstanceTelemetryEvents;

        [ImportingConstructor]
        public VsInstancePowershellTelemetryEmitter(INuGetTelemetryCollector nuGetTelemetryCollector)
        {
            _vsSolutionTelemetryEmitQueue = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());
            _vsInstanceTelemetryEmitQueue = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());
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
                EnqueueVSSolutionPowershellTelemetry();

                // Add other telemetry type queuing here in the future. You can emit many different types of telemetry other than powershell here.
                // Each of them differentiate by prefix. i.e vs.nuget.nugetpowershell.xxxx here nugetpowershell (NugetPowershellPrefix) differentiating prefix.
                // Using prefix avoid collision of property names from different types of telemetry.

                // Actual emit
                CombineAndEmitTelemetry(_vsSolutionTelemetryEmitQueue.Value, NuGetVSSolutionClose);

                _nuGetTelemetryCollector.ClearSolutionTelemetryEvents();
                _vsSolutionTelemetryEmitQueue.Value.Clear();
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

                EnqueueVSInstancePowershellTelemetry();

                // Add other telemetry type queuing here in the future. You can emit many different types of telemetry here.
                // Each of them differentiate by prefix. i.e vs.nuget.nugetpowershell.xxxx here nugetpowershell (NugetPowershellPrefix) differentiating prefix.
                // Using prefix avoid collision of property names from different types of telemetry.

                CombineAndEmitTelemetry(_vsInstanceTelemetryEmitQueue.Value, NuGetVSInstanceClose);
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
            var nuGetPowerShellLoadedEvent = _nuGetTelemetryCollector.GetSolutionTelemetryEvents().FirstOrDefault(e => e.Name == VsInstanceTelemetryConsts.NuGetPowerShellLoaded);

            if (nuGetPowerShellLoadedEvent != null)
            {
                TelemetryActivity.EmitTelemetryEvent(nuGetPowerShellLoadedEvent);
                //vsSolutionTelemetryEvents.Remove(nuGetPowerShellLoadedEvent);
            }

            // If there is not emitted PowerShellExecuteCommand telemetry.
            if (_nuGetTelemetryCollector.GetSolutionTelemetryEvents().Any(e => e.Name == PowerShellExecuteCommand))
            {
                SolutionClosedEmit();
            }
        }

        private void EnqueueVSSolutionPowershellTelemetry()
        {
            var vsSolutionPowershellTelemetry = _nuGetTelemetryCollector.GetSolutionTelemetryEvents().FirstOrDefault(e => e.Name == PowerShellExecuteCommand);

            // If powershell(PMC/PMUI) is not loaded at all then we need to create default telemetry event which will be emitted.
            if (vsSolutionPowershellTelemetry == null)
            {
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMCExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMUIExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetCommandUsed, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadPMC, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadPMUI, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadedFromPMCFirst, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + FirstTimeLoadedFromPMUI, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + FirstTimeLoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + LoadedFromPMUI, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + LoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + SolutionLoaded, true);
            }
            else
            {
                // PMC opened, but no command executed nor any solution was loaded. Rather than sending separate nugetvssolutionclose telemetry with no data just ignore.
                if (!(bool)vsSolutionPowershellTelemetry[SolutionLoaded] && (int)vsSolutionPowershellTelemetry[NuGetPMCExecuteCommandCount] == 0)
                {
                    return;
                }

                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMCExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetPMCExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMUIExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetPMUIExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetCommandUsed, vsSolutionPowershellTelemetry[NuGetCommandUsed]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadPMC, vsSolutionPowershellTelemetry[InitPs1LoadPMC]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadPMUI, vsSolutionPowershellTelemetry[InitPs1LoadPMUI]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + InitPs1LoadedFromPMCFirst, vsSolutionPowershellTelemetry[InitPs1LoadedFromPMCFirst]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + FirstTimeLoadedFromPMUI, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMUI]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + FirstTimeLoadedFromPMC, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + LoadedFromPMUI, vsSolutionPowershellTelemetry[LoadedFromPMUI]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + LoadedFromPMC, vsSolutionPowershellTelemetry[LoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + SolutionLoaded, vsSolutionPowershellTelemetry[SolutionLoaded]);
            }

            _vsSolutionTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMCWindowLoadCount, _nuGetTelemetryCollector.GetSolutionTelemetryEvents().Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount]));
        }

        private void EnqueueVSInstancePowershellTelemetry()
        {
            // Whether PMC window re-open at start by default next time VS open?
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + ReOpenAtStart, _vsInstanceTelemetryEvents.Value.Where(e => e[ReOpenAtStart] != null).Any());
            // PMC Window load count
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMCWindowLoadCount, _vsInstanceTelemetryEvents.Value.Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount]));
            // PMC number of commands executed.
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMCExecuteCommandCount, _vsInstanceTelemetryEvents.Value.Where(e => e[NuGetPMCExecuteCommandCount] is int).Sum(e => (int)e[NuGetPMCExecuteCommandCount]));
            // PMUI number of powershell commands executed.
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + NuGetPMUIExecuteCommandCount, _vsInstanceTelemetryEvents.Value.Where(e => e[NuGetPMUIExecuteCommandCount] is int).Sum(e => (int)e[NuGetPMUIExecuteCommandCount]));
            // Number of actual solutions loaded during VS instance duration.
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + SolutionCount, _solutionCount);
            // Number of solutions solutions where PMC PowerShellHost was loaded.
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + PMCPowerShellLoadedSolutionCount, _vsInstanceTelemetryEvents.Value.Where(e => e[SolutionLoaded] is bool && (bool)e[SolutionLoaded] && e[LoadedFromPMC] is bool).Count(e => (bool)e[LoadedFromPMC] == true));
            // Number of solutions solutions where PMUI PowerShellHost was loaded.
            _vsInstanceTelemetryEmitQueue.Value.Add(NuGetPowershellPrefix + PMUIPowerShellLoadedSolutionCount, _vsInstanceTelemetryEvents.Value.Where(e => e[LoadedFromPMUI] is bool).Count(e => (bool)e[LoadedFromPMUI] == true));
        }

        // Instead of emitting one by one we combine them into single event and each event is a property of this single event.
        // Currently we emit vs.nuget.NugetVSSolutionClose, vs.nuget.NugetVSInstanceClose events.
        private void CombineAndEmitTelemetry(Dictionary<string, object> telemetryEvents, string telemetryType)
        {
            // No event to emit
            if (!telemetryEvents.Keys.Any())
            {
                return;
            }

            var vsSolutionCloseTelemetry = new TelemetryEvent(telemetryType, new Dictionary<string, object>());

            foreach (KeyValuePair<string, object> telemetryEvent in telemetryEvents)
            {
                vsSolutionCloseTelemetry[telemetryEvent.Key] = telemetryEvent.Value;
            }

            TelemetryActivity.EmitTelemetryEvent(vsSolutionCloseTelemetry);
        }
    }
}
