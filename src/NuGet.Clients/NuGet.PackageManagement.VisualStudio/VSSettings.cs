// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using NuGet.Common.Telemetry;
using NuGet.Configuration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISettings))]
    [Export(typeof(INuGetSolutionTelemetry))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VSSettings : ISettings, INuGetSolutionTelemetry, IDisposable
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";
        // to initialize SolutionSettings first time outside MEF constructor
        private Tuple<string, Microsoft.VisualStudio.Threading.AsyncLazy<ISettings>> _solutionSettings;
        // PMC, PMUI powershell telemetry consts
        private const string NugetPowershellPrefix = "NugetPowershell."; // Using prefix prevent accidental same name property collission from different type telemetry.
        private const string NugetVSSolutionClose = "NugetVSSolutionClose";
        private const string NugetVSInstanceClose = "NugetVSInstanceClose";
        private const string PowerShellExecuteCommand = "PowerShellExecuteCommand";
        private const string NuGetPMCExecuteCommandCount = "NuGetPMCExecuteCommandCount";
        private const string NuGetNonPMCExecuteCommandCount = "NuGetNonPMCExecuteCommandCount";
        private const string LoadedFromPMC = "LoadedFromPMC";
        private const string FirstTimeLoadedFromPMC = "FirstTimeLoadedFromPMC";
        private const string LoadedFromPMUI = "LoadedFromPMUI";
        private const string FirstTimeLoadedFromPMUI = "FirstTimeLoadedFromPMUI";
        // PMC UI Console Container telemetry consts
        private const string NuGetPMCWindowLoadCount = "NuGetPMCWindowLoadCount";
        private const string ReOpenAtStart = "ReOpenAtStart";
        // _solutionTelemetryEvents hold telemetry for current VS solution session.
        private List<Common.TelemetryEvent> _vsSolutionTelemetryEvents;
        private Dictionary<string, object> _vsSolutionTelemetryEmitQueue;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private List<Common.TelemetryEvent> _vsInstanceTelemetryEvents;
        private Dictionary<string, object> _vsInstanceTelemetryEmitQueue;
        private const string SolutionCount = "SolutionCount";
        private const string PMCPowershellLoadedSolutionCount = "PMCPowershellLoadedSolutionCount";
        private const string PMUIPowershellLoadedSolutionCount = "PMUIPowershellLoadedSolutionCount";
        private const string SolutionLoaded = "SolutionLoaded";
        private int _solutionCount;

        private ISettings SolutionSettings
        {
            get
            {
                if (_solutionSettings == null)
                {
                    // first time set _solutionSettings via ResetSolutionSettings API call.
                    ResetSolutionSettingsIfNeeded();
                }

                return NuGetUIThreadHelper.JoinableTaskFactory.Run(_solutionSettings.Item2.GetValueAsync);
            }
        }

        private ISolutionManager SolutionManager { get; set; }

        private IMachineWideSettings MachineWideSettings { get; set; }

        public event EventHandler SettingsChanged;

        public VSSettings(ISolutionManager solutionManager)
            : this(solutionManager, machineWideSettings: null)
        {
        }

        [ImportingConstructor]
        public VSSettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings)
        {
            SolutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            MachineWideSettings = machineWideSettings;
            SolutionManager.SolutionOpening += OnSolutionOpening;
            SolutionManager.SolutionOpened += OnSolutionOpened;
            SolutionManager.SolutionClosed += OnSolutionClosed;
            _vsSolutionTelemetryEvents = new List<Common.TelemetryEvent>();
            _vsSolutionTelemetryEmitQueue = new Dictionary<string, object>();
            _vsInstanceTelemetryEvents = new List<Common.TelemetryEvent>();
            _vsInstanceTelemetryEmitQueue = new Dictionary<string, object>();
        }

        private bool ResetSolutionSettingsIfNeeded()
        {
            string root;
            if (SolutionManager == null
                || !SolutionManager.IsSolutionOpen
                || string.IsNullOrEmpty(SolutionManager.SolutionDirectory))
            {
                root = null;
            }
            else
            {
                root = Path.Combine(SolutionManager.SolutionDirectory, NuGetSolutionSettingsFolder);
            }

            // This is a performance optimization.
            // The solution load/unload events are called in the UI thread and are used to reset the settings.
            // In some cases there's a synchronous dependency between the invocation of the Solution event and the settings being reset.
            // In the open PM UI scenario (no restore run), there is an asynchronous invocation of this code path. This changes ensures that
            // the synchronous calls that come after the asynchrnous calls don't do duplicate work.
            // That however is not the case for solution close and  same session close -> open events. Those will be on the UI thread.
            if (_solutionSettings == null || !string.Equals(root, _solutionSettings.Item1))
            {
                _solutionSettings = new Tuple<string, Microsoft.VisualStudio.Threading.AsyncLazy<ISettings>>(
                    item1: root,
                    item2: new Microsoft.VisualStudio.Threading.AsyncLazy<ISettings>(async () =>
                        {
                            ISettings settings = null;
                            try
                            {
                                settings = Settings.LoadDefaultSettings(root, configFileName: null, machineWideSettings: MachineWideSettings);
                            }
                            catch (NuGetConfigurationException ex)
                            {
                                settings = NullSettings.Instance;
                                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                MessageHelper.ShowErrorMessage(Common.ExceptionUtilities.DisplayMessage(ex), Strings.ConfigErrorDialogBoxTitle);
                            }

                            return settings;

                        }, NuGetUIThreadHelper.JoinableTaskFactory));
                return true;
            }

            return false;
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            DetectSolutionSettingChange();
        }

        private void DetectSolutionSettingChange()
        {
            var hasChanged = ResetSolutionSettingsIfNeeded();

            if (hasChanged)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSolutionOpening(object sender, EventArgs e)
        {
            DetectSolutionSettingChange();
        }

        private void OnSolutionOpened(object sender, EventArgs e)
        {
            // PMC used before any solution is loaded, let's emit what we have before loading a solution.
            if (_solutionCount == 0 && _vsSolutionTelemetryEvents.Any(e => e[NuGetPMCWindowLoadCount] is int && (int)e[NuGetPMCWindowLoadCount] > 0))
            {
                EmitVSSolutionTelemetry();
            }

            _solutionCount++;
        }

        private void OnSolutionClosed(object sender, EventArgs e)
        {
            EmitVSSolutionTelemetry();
            DetectSolutionSettingChange();
        }

        public SettingSection GetSection(string sectionName)
        {
            return SolutionSettings.GetSection(sectionName);
        }

        public void AddOrUpdate(string sectionName, SettingItem item)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.AddOrUpdate(sectionName, item);
            }
        }

        public void Remove(string sectionName, SettingItem item)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.Remove(sectionName, item);
            }
        }

        public void SaveToDisk()
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SaveToDisk();
            }
        }

        public IList<string> GetConfigFilePaths() => SolutionSettings.GetConfigFilePaths();

        public IList<string> GetConfigRoots() => SolutionSettings.GetConfigRoots();

        public void Dispose()
        {
            SolutionManager.SolutionOpening -= OnSolutionOpening;
            SolutionManager.SolutionOpened -= OnSolutionOpened;
            SolutionManager.SolutionClosed -= OnSolutionClosed;

            EmitVSInstanceTelemetry();
        }

        public void AddSolutionTelemetryEvent(Common.TelemetryEvent telemetryData)
        {
            _vsSolutionTelemetryEvents.Add(telemetryData);
            _vsInstanceTelemetryEvents.Add(telemetryData);
        }

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);

        // Emit VS solution session telemetry when solution is closed.
        private void EmitVSSolutionTelemetry()
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
            {}
        }

        private void EnqueueVSSolutionPowershellTelemetry()
        {
            var vsSolutionPowershellTelemetry = _vsSolutionTelemetryEvents.FirstOrDefault(e => e.Name == PowerShellExecuteCommand);

            // If powershell(PMC/PMUI) is not loaded at all then we need to create default telemetry event which will be emitted.
            if (vsSolutionPowershellTelemetry == null)
            {
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetNonPMCExecuteCommandCount, 0);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMUI, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMC, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMUI, false);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + SolutionLoaded, _solutionCount > 0); // If 'false' : PMC used before any solution is loaded.
            }
            else
            {
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetPMCExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetNonPMCExecuteCommandCount, vsSolutionPowershellTelemetry[NuGetNonPMCExecuteCommandCount]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMC, vsSolutionPowershellTelemetry[LoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + LoadedFromPMUI, vsSolutionPowershellTelemetry[LoadedFromPMUI]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMC, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMC]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + FirstTimeLoadedFromPMUI, vsSolutionPowershellTelemetry[FirstTimeLoadedFromPMUI]);
                _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + SolutionLoaded, _solutionCount > 0); // If 'false' : PMC used before any solution is loaded.
            }

            _vsSolutionTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCWindowLoadCount, _vsSolutionTelemetryEvents.Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount]));
        }

        // Emit VS solution session telemetry when VS instance is closed.
        private void EmitVSInstanceTelemetry()
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
            {}
        }

        private void EnqueueVSInstancePowershellTelemetry()
        {
            // Edge case. PMC can be used without loading VS solution at all.
            if (_vsSolutionTelemetryEvents.Any(e => e.Name == PowerShellExecuteCommand))
            {
                EmitVSSolutionTelemetry();
            }

            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + ReOpenAtStart, _vsInstanceTelemetryEvents.Where(e => e[ReOpenAtStart] != null).Any()); // Whether PMC window re-open at start by default next time VS open?
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCWindowLoadCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetPMCWindowLoadCount] is int).Sum(e => (int)e[NuGetPMCWindowLoadCount])); // PMC Window load count
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetPMCExecuteCommandCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetPMCExecuteCommandCount] is int).Sum(e => (int)e[NuGetPMCExecuteCommandCount])); // PMC number of commands executed.
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + NuGetNonPMCExecuteCommandCount, _vsInstanceTelemetryEvents.Where(e => e[NuGetNonPMCExecuteCommandCount] is int).Sum(e => (int)e[NuGetNonPMCExecuteCommandCount])); // PMUI number of powershell commands executed.
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + SolutionCount, _solutionCount);
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + PMCPowershellLoadedSolutionCount, _vsInstanceTelemetryEvents.Where(e => e[SolutionLoaded] is bool && (bool)e[SolutionLoaded] && e[LoadedFromPMC] is bool).Count(e => (bool)e[LoadedFromPMC] == true)); // SolutionLoaded used here to remove edge case : PMC used before any solution is loaded 
            _vsInstanceTelemetryEmitQueue.Add(NugetPowershellPrefix + PMUIPowershellLoadedSolutionCount, _vsInstanceTelemetryEvents.Where(e => e[LoadedFromPMUI] is bool).Count(e => (bool)e[LoadedFromPMUI] == true));
        }

        // Instead of emitting one by one we combine them into single event and each event is a property of this single event.
        // Currently we emit vs.nuget.NugetVSSolutionClose, vs.nuget.NugetVSInstanceClose events.
        private void CombineAndEmitTelemetry(Dictionary<string, object> telemetryEvents, string telemetryType)
        {
            var vsSolutionCloseTelemetry = new Common.TelemetryEvent(telemetryType, new Dictionary<string, object>());

            foreach (KeyValuePair<string, object> telemetryEvent in telemetryEvents)
            {
                vsSolutionCloseTelemetry[telemetryEvent.Key] = telemetryEvent.Value;
            }

            Common.TelemetryActivity.EmitTelemetryEvent(vsSolutionCloseTelemetry);
        }
    }
}
