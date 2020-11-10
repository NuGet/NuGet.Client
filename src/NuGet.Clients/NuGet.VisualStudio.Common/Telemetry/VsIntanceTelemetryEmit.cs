// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(INuGetTelemetryAggregator))]
    [Export(typeof(VsIntanceTelemetryEmit))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsIntanceTelemetryEmit : VsInstanceTelemetryConsts, INuGetTelemetryAggregator
    {
        // _solutionTelemetryEvents hold telemetry for current VS solution session.
        private List<TelemetryEvent> _vsSolutionTelemetryEvents;
        private Dictionary<string, object> _vsSolutionTelemetryEmitQueue;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private List<TelemetryEvent> _vsInstanceTelemetryEvents;
        private Dictionary<string, object> _vsInstanceTelemetryEmitQueue;

        private int _solutionCount;

        public VsIntanceTelemetryEmit()
        {
            _vsSolutionTelemetryEvents = new List<TelemetryEvent>();
            _vsSolutionTelemetryEmitQueue = new Dictionary<string, object>();
            _vsInstanceTelemetryEvents = new List<TelemetryEvent>();
            _vsInstanceTelemetryEmitQueue = new Dictionary<string, object>();
        }

        public void AddSolutionTelemetryEvent(TelemetryEvent telemetryData)
        {
            _vsSolutionTelemetryEvents.Add(telemetryData);
            _vsInstanceTelemetryEvents.Add(telemetryData);
        }

        public void SolutionOpenedEmit()
        {
            // PMC used before any solution is loaded, let's emit what we have before loading a solution.
            // Used means at least one powershell command executed, otherwise telemetry(NuGetPMCWindowLoadCount and FirstTimeLoadedFromPMC) is merged with first opened solution metric rather than sending separate nugetvssolutionclose telemetry with no data.
            if (_solutionCount == 0 && _vsSolutionTelemetryEvents.Any(e => e[VsInstanceTelemetryConsts.NuGetPMCExecuteCommandCount] is int && (int)e[NuGetPMCExecuteCommandCount] > 0))
            {
                EmitVSSolutionTelemetry();
            }

            _solutionCount++;
        }

        // Emit VS solution session telemetry when solution is closed.
        public void EmitVSSolutionTelemetry()
        {
            try
            {
                // Queue all different types of telemetries and do some processing prior to emit.
                EnqueueVSSolutionPowershellTelemetry();

                // Add other telemetry types here in the future. You can emit many different types of telemetry other than powershell here.
                // Each of them differentiate by prefix. i.e vs.nuget.nugetpowershell.xxxx here nugetpowershell (NugetPowershellPrefix) differentiating prefix.
                // Using prefix avoid collision of property names from different types of telemetry.

                _vsSolutionTelemetryEvents.Clear();

                // Actual emit
                CombineAndEmitTelemetry(_vsSolutionTelemetryEmitQueue, NugetVSSolutionClose);
                _vsSolutionTelemetryEmitQueue.Clear();
            }
            catch (Exception)
            {
                // Currently do nothing.
            }
        }

        private void EnqueueVSSolutionPowershellTelemetry()
        {
            var vsSolutionPowershellTelemetry = _vsSolutionTelemetryEvents.FirstOrDefault(e => e.Name == PowerShellExecuteCommand);

            // If powershell(PMC/PMUI) is not loaded at all then we need to create default telemetry event which will be emitted.
            if (vsSolutionPowershellTelemetry == null)
            {
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMUIExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetCommandUsed, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + InitPs1Loaded, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMUI, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMUI, false);
            }
            else
            {
                // PMC opened, but no command executed nor any solution opened. Rather than sending separate nugetvssolutionclose telemetry with no data just ignore.
                if (_solutionCount == 0 && (int)vsSolutionPowershellTelemetry[NuGetPMCExecuteCommandCount] == 0)
                {
                    return;
                }

                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetPMCExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMUIExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetPMUIExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetCommandUsed, vsSolutionPowershellTelemetry[NuGetCommandUsed]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + InitPs1Loaded, vsSolutionPowershellTelemetry[InitPs1Loaded]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMC, vsSolutionPowershellTelemetry[LoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMUI, vsSolutionPowershellTelemetry[LoadedFromPMUI]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMC, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMUI, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMUI]);
            }

            _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCWindowLoadCount, _vsSolutionTelemetryEvents.Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount]));
        }

        // Emit VS solution session telemetry when VS instance is closed.
        public void EmitVSInstanceTelemetry()
        {
            try
            {
                EnqueueVSInstancePowershellTelemetry();
                // Add other telemetry types here in the future. You can emit many different types of telemetry here.
                // Each of them differentiate by prefix. i.e vs.nuget.nugetpowershell.xxxx here nugetpowershell (NugetPowershellPrefix) differentiating prefix.
                // Using prefix avoid collision of property names from different types of telemetry.

                CombineAndEmitTelemetry(_vsInstanceTelemetryEmitQueue, NugetVSInstanceClose);
            }
            catch (Exception)
            {
                // Currently do nothing.
            }
        }

        private void EnqueueVSInstancePowershellTelemetry()
        {
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + ReOpenAtStart, _vsInstanceTelemetryEvents.Where(e => e[ReOpenAtStart] != null).Any()); // Whether PMC window re-open at start by default next time VS open?
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCWindowLoadCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount])); // PMC Window load count
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetPMCExecuteCommandCount] is int).Sum(e => (int)e[NuGetPMCExecuteCommandCount])); // PMC number of commands executed.
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMUIExecuteCommandCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetPMUIExecuteCommandCount] is int).Sum(e => (int)e[NuGetPMUIExecuteCommandCount])); // PMUI number of powershell commands executed.
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + SolutionCount, _solutionCount);
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + PMCPowerShellLoadedSolutionCount, _vsInstanceTelemetryEvents.Where(e => e[SolutionLoaded] is bool && (bool)e[SolutionLoaded] && e[LoadedFromPMC] is bool).Count(e => (bool)e[LoadedFromPMC] == true)); // SolutionLoaded used here to remove edge case : PMC used before any solution is loaded 
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + PMUIPowerShellLoadedSolutionCount, _vsInstanceTelemetryEvents.Where(e => e[LoadedFromPMUI] is bool).Count(e => (bool)e[LoadedFromPMUI] == true));
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
