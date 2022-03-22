// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    [Export(typeof(INuGetFeatureFlagService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class NuGetFeatureFlagService : INuGetFeatureFlagService
    {
        private readonly IEnvironmentVariableReader _environmentVariableReader;
        private readonly IAsyncServiceProvider _asyncServiceProvider;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<IVsFeatureFlags> _ivsFeatureFlags;
        private readonly Dictionary<string, bool> _featureFlagCache;

        [ImportingConstructor]
        public NuGetFeatureFlagService()
            : this(EnvironmentVariableWrapper.Instance, AsyncServiceProvider.GlobalProvider)
        {
            // ensure uniqueness.
        }

        internal NuGetFeatureFlagService(IEnvironmentVariableReader environmentVariableReader, IAsyncServiceProvider asyncServiceProvider)
        {
            _environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
            _asyncServiceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
            _ivsFeatureFlags = new(() => _asyncServiceProvider.GetServiceAsync<SVsFeatureFlags, IVsFeatureFlags>(), NuGetUIThreadHelper.JoinableTaskFactory);
            _featureFlagCache = new();
        }

        public async Task<bool> IsFeatureEnabledAsync(NuGetFeatureFlagConstants experiment)
        {
            GetEnvironmentVariablesForFeature(experiment, out bool isFeatureForcedEnabled, out bool isFeatureForcedDisabled);
            // Perform the check from the feature flag service only once.
            // There are events sent for targetted notification changes, but we don't listen to those at this point.
            if (!_featureFlagCache.TryGetValue(experiment.FeatureFlagName, out bool featureEnabled))
            {
                var featureFlagService = await _ivsFeatureFlags.GetValueAsync();
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                featureEnabled = featureFlagService.IsFeatureEnabled(experiment.FeatureFlagName, defaultValue: experiment.DefaultFeatureFlag);
                _featureFlagCache.Add(experiment.FeatureFlagName, featureEnabled);
            }
            return !isFeatureForcedDisabled && (isFeatureForcedEnabled || featureEnabled);
        }

        private void GetEnvironmentVariablesForFeature(NuGetFeatureFlagConstants experiment, out bool isExpForcedEnabled, out bool isExpForcedDisabled)
        {
            isExpForcedEnabled = false;
            isExpForcedDisabled = false;
            if (!string.IsNullOrEmpty(experiment.FeatureEnvironmentVariable))
            {
                string envVarOverride = _environmentVariableReader.GetEnvironmentVariable(experiment.FeatureEnvironmentVariable);

                isExpForcedDisabled = envVarOverride == "0";
                isExpForcedEnabled = envVarOverride == "1";
            }
        }
    }
}
