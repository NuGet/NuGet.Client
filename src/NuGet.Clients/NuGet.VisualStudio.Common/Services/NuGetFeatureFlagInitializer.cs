// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NuGet.Shared;

namespace NuGet.VisualStudio
{
    [Export(typeof(INuGetFeatureFlagInitializer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class NuGetFeatureFlagInitializer : INuGetFeatureFlagInitializer
    {
        private readonly INuGetFeatureFlagService _featureFlagService;
        private readonly INuGetExperimentationService _experimentationService;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<bool> _initialization;

        [ImportingConstructor]
        public NuGetFeatureFlagInitializer(
            INuGetFeatureFlagService featureFlagService,
            INuGetExperimentationService experimentationService)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _experimentationService = experimentationService ?? throw new ArgumentNullException(nameof(experimentationService));
            _initialization = new(InitializeCoreAsync, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public Task InitializeAsync() => _initialization.GetValueAsync();

        private async Task<bool> InitializeCoreAsync()
        {
            // Publish switches here for NuGetFeatureFlags consumers that cannot depend on VS services.
            // Consumers must read switches dynamically, not cache values before initialization.
            if (_experimentationService.IsExperimentEnabled(ExperimentationConstants.UseSystemTextJsonDeserialization))
            {
                AppContext.SetSwitch(NuGetFeatureFlags.UseSystemTextJsonDeserializationSwitchName, isEnabled: true);
            }

            bool enabled = await _featureFlagService.IsFeatureEnabledAsync(NuGetFeatureFlagConstants.ControlPowerShellScriptExecution);
            AppContext.SetSwitch(NuGetFeatureFlags.ControlPowerShellScriptExecutionSwitchName, enabled);
            return true;
        }
    }
}
