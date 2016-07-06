// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace NuGet.PackageManagement.UI
{
    public static class ExperimentalFeatures
    {
        private const string SettingsStorePath = @"NuGet";
        private const string ExperimentalFeaturesPropertyName = "ExperimentalFeatures";

        public static event EventHandler<EnabledChangedEventArgs> EnabledChanged;

        public static bool IsEnabled
        {
            get
            {
                var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
                var settingsStore = settingsManager.GetReadOnlySettingsStore(SettingsScope.UserSettings);
                EnsureNuGetSettingsCollectionExists();
                return settingsStore.GetBoolean(SettingsStorePath, ExperimentalFeaturesPropertyName);
            }
            set
            {
                // This is stored as a Visual Studio settings so we can use it in a UIContext rule.
                var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
                var settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
                EnsureNuGetSettingsCollectionExists(settingsStore);
                settingsStore.SetBoolean(SettingsStorePath, ExperimentalFeaturesPropertyName, value);
                OnEnabledChanged(value);
            }
        }

        private static void EnsureNuGetSettingsCollectionExists(WritableSettingsStore settingsStore = null)
        {
            if (settingsStore == null)
            {
                var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
                settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            }

            if (!settingsStore.CollectionExists(SettingsStorePath))
            {
                settingsStore.CreateCollection(SettingsStorePath);
            }

            if (!settingsStore.PropertyExists(SettingsStorePath, ExperimentalFeaturesPropertyName))
            {
                settingsStore.SetBoolean(SettingsStorePath, ExperimentalFeaturesPropertyName, false);
            }
        }

        private static void OnEnabledChanged(bool enabled)
        {
            EnabledChanged?.Invoke(null, new EnabledChangedEventArgs(enabled));
        }
    }

    public class EnabledChangedEventArgs
    {
        public bool Enabled { get; }

        public EnabledChangedEventArgs(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
