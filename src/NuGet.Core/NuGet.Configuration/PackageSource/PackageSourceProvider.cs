// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.Configuration
{
    public class PackageSourceProvider : IPackageSourceProvider
    {
        private const int MaxSupportedProtocolVersion = 3;

        private ISettings Settings { get; set; }
        private readonly IDictionary<PackageSource, PackageSource> _migratePackageSources;
        private readonly IEnumerable<PackageSource> _configurationDefaultSources;

        public PackageSourceProvider(ISettings settings)
            : this(settings, migratePackageSources: null)
        {
        }

        public PackageSourceProvider(
          ISettings settings,
          IDictionary<PackageSource, PackageSource> migratePackageSources)
            : this(settings,
                  migratePackageSources,
                  ConfigurationDefaults.Instance.DefaultPackageSources)
        {
        }

        public PackageSourceProvider(
            ISettings settings,
            IDictionary<PackageSource, PackageSource> migratePackageSources,
            IEnumerable<PackageSource> configurationDefaultSources
            )
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            Settings = settings;
            Settings.SettingsChanged += (_, __) => { OnPackageSourcesChanged(); };
            _migratePackageSources = migratePackageSources;
            _configurationDefaultSources = LoadConfigurationDefaultSources(configurationDefaultSources);
        }

        private IEnumerable<PackageSource> LoadConfigurationDefaultSources(IEnumerable<PackageSource> configurationDefaultSources)
        {
#if !DNXCORE50
            // Global default NuGet source doesn't make sense on Mono
            if (RuntimeEnvironmentHelper.IsMono)
            {
                return Enumerable.Empty<PackageSource>();
            }
#endif
            var packageSourceLookup = new Dictionary<string, IndexedPackageSource>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;

            foreach (var packageSource in configurationDefaultSources)
            {
                packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource);
            }

            return packageSourceLookup.Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource);
        }

        /// <summary>
        /// Returns PackageSources if specified in the config file. Else returns the default sources specified in the
        /// constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageSource> LoadPackageSources()
        {
            var settingsValue = new List<SettingValue>();
            var sourceSettingValues = Settings.GetSettingValues(ConfigurationConstants.PackageSources, isPath: true) ??
                                      Enumerable.Empty<SettingValue>();

            // Order the list so that they are ordered in priority order
            var settingValues = sourceSettingValues.OrderByDescending(setting => setting.Priority);

            // get list of disabled packages
            var disabledSetting = Settings.GetSettingValues(ConfigurationConstants.DisabledPackageSources) ?? Enumerable.Empty<SettingValue>();

            var disabledSources = new Dictionary<string, SettingValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in disabledSetting)
            {
                if (disabledSources.ContainsKey(setting.Key))
                {
                    disabledSources[setting.Key] = setting;
                }
                else
                {
                    disabledSources.Add(setting.Key, setting);
                }
            }

            var packageSourceLookup = new Dictionary<string, IndexedPackageSource>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;
            foreach (var setting in settingValues)
            {
                var name = setting.Key;

                var isEnabled = true;
                SettingValue disabledSource;
                if (disabledSources.TryGetValue(name, out disabledSource))
                {
                    isEnabled = false;
                }

                var packageSource = ReadPackageSource(setting, isEnabled);
                packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource);
            }

            var loadedPackageSources = packageSourceLookup.Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource)
                .ToList();

            if (_migratePackageSources != null)
            {
                MigrateSources(loadedPackageSources);
            }

            return loadedPackageSources;
        }

        private PackageSource ReadPackageSource(SettingValue setting, bool isEnabled)
        {
            var name = setting.Key;
            var packageSource = new PackageSource(setting.Value, name, isEnabled)
            {
                IsMachineWide = setting.IsMachineWide
            };

            var credentials = ReadCredential(name);
            if (credentials != null)
            {
                packageSource.UserName = credentials.Username;
                packageSource.PasswordText = credentials.PasswordText;
                packageSource.IsPasswordClearText = credentials.IsPasswordClearText;
            }

            packageSource.ProtocolVersion = ReadProtocolVersion(setting);
            packageSource.Origin = setting.Origin;

            return packageSource;
        }

        private static int ReadProtocolVersion(SettingValue setting)
        {
            string protocolVersionString;
            int protocolVersion;
            if (setting.AdditionalData.TryGetValue(ConfigurationConstants.ProtocolVersionAttribute, out protocolVersionString)
                &&
                int.TryParse(protocolVersionString, out protocolVersion))
            {
                return protocolVersion;
            }

            return PackageSource.DefaultProtocolVersion;
        }

        private static int AddOrUpdateIndexedSource(
            Dictionary<string, IndexedPackageSource> packageSourceLookup,
            int packageIndex,
            PackageSource packageSource)
        {
            IndexedPackageSource previouslyAddedSource;
            if (!packageSourceLookup.TryGetValue(packageSource.Name, out previouslyAddedSource))
            {
                packageSourceLookup[packageSource.Name] = new IndexedPackageSource
                {
                    PackageSource = packageSource,
                    Index = packageIndex++
                };
            }
            else if (previouslyAddedSource.PackageSource.ProtocolVersion < packageSource.ProtocolVersion
                     &&
                     packageSource.ProtocolVersion <= MaxSupportedProtocolVersion)
            {
                // Pick the package source with the highest supported protocol version
                previouslyAddedSource.PackageSource = packageSource;
            }

            return packageIndex;
        }

        private PackageSourceCredential ReadCredential(string sourceName)
        {
            var environmentCredentials = ReadCredentialFromEnvironment(sourceName);

            if (environmentCredentials != null)
            {
                return environmentCredentials;
            }

            var values = Settings.GetNestedValues(ConfigurationConstants.CredentialsSectionName, sourceName);
            if (values != null
                && values.Any())
            {
                var userName = values.FirstOrDefault(k => k.Key.Equals(ConfigurationConstants.UsernameToken, StringComparison.OrdinalIgnoreCase)).Value;

                if (!String.IsNullOrEmpty(userName))
                {
                    var encryptedPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase)).Value;
                    if (!String.IsNullOrEmpty(encryptedPassword))
                    {
                        return new PackageSourceCredential(userName, encryptedPassword, isPasswordClearText: false);
                    }

                    var clearTextPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationConstants.ClearTextPasswordToken, StringComparison.Ordinal)).Value;
                    if (!String.IsNullOrEmpty(clearTextPassword))
                    {
                        return new PackageSourceCredential(userName, clearTextPassword, isPasswordClearText: true);
                    }
                }
            }
            return null;
        }

        private PackageSourceCredential ReadCredentialFromEnvironment(string sourceName)
        {
            var rawCredentials = Environment.GetEnvironmentVariable("NuGetPackageSourceCredentials_" + sourceName);
            if (string.IsNullOrEmpty(rawCredentials))
            {
                return null;
            }

            var match = Regex.Match(rawCredentials.Trim(), @"^Username=(?<user>.*?);\s*Password=(?<pass>.*?)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return new PackageSourceCredential(match.Groups["user"].Value, match.Groups["pass"].Value, true);
        }

        private void MigrateSources(List<PackageSource> loadedPackageSources)
        {
            var hasChanges = false;
            var packageSourcesToBeRemoved = new List<PackageSource>();

            // doing migration
            for (var i = 0; i < loadedPackageSources.Count; i++)
            {
                var ps = loadedPackageSources[i];
                PackageSource targetPackageSource;
                if (_migratePackageSources.TryGetValue(ps, out targetPackageSource))
                {
                    if (loadedPackageSources.Any(p => p.Equals(targetPackageSource)))
                    {
                        packageSourcesToBeRemoved.Add(loadedPackageSources[i]);
                    }
                    else
                    {
                        loadedPackageSources[i] = targetPackageSource.Clone();
                        // make sure we preserve the IsEnabled property when migrating package sources
                        loadedPackageSources[i].IsEnabled = ps.IsEnabled;
                    }
                    hasChanges = true;
                }
            }

            foreach (var packageSource in packageSourcesToBeRemoved)
            {
                loadedPackageSources.Remove(packageSource);
            }

            if (hasChanges)
            {
                SavePackageSources(loadedPackageSources);
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            // clear the old values
            // and write the new ones
            var sourcesToWrite = sources.Where(s => !s.IsMachineWide && s.IsPersistable);

            var existingSettings = (Settings.GetSettingValues(ConfigurationConstants.PackageSources, isPath: true) ??
                                    Enumerable.Empty<SettingValue>()).Where(setting => !setting.IsMachineWide).ToList();
            var minPriority = 0;

            // get lowest priority in existingSetting
            if (existingSettings.Count > 0)
            {
                minPriority = existingSettings.Min(setting => setting.Priority);
            }

            existingSettings.RemoveAll(setting => !sources.Any(s => string.Equals(s.Name, setting.Key, StringComparison.OrdinalIgnoreCase)));
            var existingSettingsLookup = existingSettings.ToLookup(setting => setting.Key, StringComparer.OrdinalIgnoreCase);
            var existingDisabledSources = Settings.GetSettingValues(ConfigurationConstants.DisabledPackageSources) ??
                                          Enumerable.Empty<SettingValue>();
            var existingDisabledSourcesLookup = existingDisabledSources.ToLookup(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

            var sourceSettings = new List<SettingValue>();
            var sourcesToDisable = new List<SettingValue>();

            foreach (var source in sourcesToWrite)
            {
                var foundSettingWithSourcePriority = false;
                var settingPriority = 0;
                var existingSettingForSource = existingSettingsLookup[source.Name];

                // Preserve packageSource entries from low priority settings.
                foreach (var existingSetting in existingSettingForSource)
                {
                    settingPriority = Math.Max(settingPriority, existingSetting.Priority);

                    // Write all settings other than the currently written one to the current NuGet.config.
                    if (ReadProtocolVersion(existingSetting) == source.ProtocolVersion)
                    {
                        // Update the source value of all settings with the same protocol version.
                        foundSettingWithSourcePriority = true;

                        // if the existing source changed, update the setting value
                        if (!existingSetting.Value.Equals(source.Source))
                        {
                            existingSetting.Value = source.Source;
                            existingSetting.OriginalValue = source.Source;
                        }
                    }
                    sourceSettings.Add(existingSetting);
                }

                if (!foundSettingWithSourcePriority)
                {
                    // This is a new source, add it to the Setting with the lowest priority.
                    // if there is a clear tag in one config file, new source will be cleared
                    // we should set new source priority to lowest existingSetting priority
                    // NOTE: origin can be null here because it isn't ever used when saving.
                    var settingValue = new SettingValue(source.Name, source.Source, origin: null, isMachineWide: false, priority: minPriority);

                    if (source.ProtocolVersion != PackageSource.DefaultProtocolVersion)
                    {
                        settingValue.AdditionalData[ConfigurationConstants.ProtocolVersionAttribute] =
                            source.ProtocolVersion.ToString(CultureInfo.InvariantCulture);
                    }

                    sourceSettings.Add(settingValue);
                }

                // settingValue contains the setting with the highest priority.

                var existingDisabledSettings = existingDisabledSourcesLookup[source.Name];
                // Preserve disabledPackageSource entries from low priority settings.
                foreach (var setting in existingDisabledSettings.Where(s => s.Priority < settingPriority))
                {
                    sourcesToDisable.Add(setting);
                }

                if (!source.IsEnabled)
                {
                    // Add an entry to the disabledPackageSource in the file that contains
                    sourcesToDisable.Add(new SettingValue(source.Name, "true", origin: null, isMachineWide: false, priority: settingPriority));
                }
            }

            // add entries to the disabledPackageSource for machine wide setting
            foreach (var source in sources.Where(s => s.IsMachineWide && !s.IsEnabled))
            {
                sourcesToDisable.Add(new SettingValue(source.Name, "true", origin: null, isMachineWide: true, priority: 0));
            }

            // add entries to the disablePackageSource for disabled package sources that are not in loaded 'sources'
            foreach (var setting in existingDisabledSources)
            {
                // The following code ensures that we do not miss to mark an existing disabled source as disabled.
                // However, ONLY mark an existing disable source setting as disabled, if,
                // 1) it is not in the list of loaded package sources, or,
                // 2) it is not already in the list of sources to disable.
                if (!sources.Any(s => string.Equals(s.Name, setting.Key, StringComparison.OrdinalIgnoreCase)) &&
                    !sourcesToDisable.Any(s => string.Equals(s.Key, setting.Key, StringComparison.OrdinalIgnoreCase)
                                            && s.Priority == setting.Priority))
                {
                    sourcesToDisable.Add(setting);
                }
            }

            // Write the updates to the nearest settings file.
            Settings.UpdateSections(ConfigurationConstants.PackageSources, sourceSettings);

            // overwrite new values for the <disabledPackageSources> section
            Settings.UpdateSections(ConfigurationConstants.DisabledPackageSources, sourcesToDisable);

            // Overwrite the <packageSourceCredentials> section
            Settings.DeleteSection(ConfigurationConstants.CredentialsSectionName);

            var sourceWithCredentials = sources.Where(s => !String.IsNullOrEmpty(s.UserName) && !String.IsNullOrEmpty(s.PasswordText));
            foreach (var source in sourceWithCredentials)
            {
                Settings.SetNestedValues(ConfigurationConstants.CredentialsSectionName, source.Name, new[]
                    {
                        new KeyValuePair<string, string>(ConfigurationConstants.UsernameToken, source.UserName),
                        ReadPasswordValues(source)
                    });
            }

            OnPackageSourcesChanged();
        }

        /// <summary>
        /// Fires event PackageSourcesChanged
        /// </summary>
        private void OnPackageSourcesChanged()
        {
            if (PackageSourcesChanged != null)
            {
                PackageSourcesChanged(this, EventArgs.Empty);
            }
        }

        private static KeyValuePair<string, string> ReadPasswordValues(PackageSource source)
        {
            try
            {
                var passwordToken = source.IsPasswordClearText ? ConfigurationConstants.ClearTextPasswordToken : ConfigurationConstants.PasswordToken;
                var passwordValue = source.IsPasswordClearText ? source.PasswordText : EncryptionUtility.EncryptString(source.PasswordText);

                return new KeyValuePair<string, string>(passwordToken, passwordValue);
            }
            catch (NotSupportedException e)
            {
                throw new NuGetConfigurationException(
                           string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedEncryptPassword, source.Source), e);
            }
        }

        public void DisablePackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            Settings.SetValue(ConfigurationConstants.DisabledPackageSources, source.Name, "true");
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var value = Settings.GetValue(ConfigurationConstants.DisabledPackageSources, source.Name);

            // It doesn't matter what value it is.
            // As long as the package source name is persisted in the <disabledPackageSources> section, the source is disabled.
            return String.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Gets the name of the ActivePackageSource from NuGet.Config
        /// </summary>
        public string ActivePackageSourceName
        {
            get
            {
                var activeSource = Settings.GetSettingValues(ConfigurationConstants.ActivePackageSourceSectionName).FirstOrDefault();
                if (activeSource == null)
                {
                    return null;
                }

                return activeSource.Key;
            }
        }

        /// <summary>
        /// Saves the <paramref name="source" /> as the active source.
        /// </summary>
        /// <param name="source"></param>
        public void SaveActivePackageSource(PackageSource source)
        {
            try
            {
                Settings.DeleteSection(ConfigurationConstants.ActivePackageSourceSectionName);
                Settings.SetValue(ConfigurationConstants.ActivePackageSourceSectionName, source.Name, source.Source);
            }
            catch (Exception)
            {
                // we want to ignore all errors here.
            }
        }

        private class PackageSourceCredential
        {
            public string Username { get; private set; }

            public string PasswordText { get; private set; }

            public bool IsPasswordClearText { get; private set; }

            public PackageSourceCredential(string username, string passwordText, bool isPasswordClearText)
            {
                Username = username;
                PasswordText = passwordText;
                IsPasswordClearText = isPasswordClearText;
            }
        }

        private class IndexedPackageSource
        {
            public int Index { get; set; }

            public PackageSource PackageSource { get; set; }
        }

        public event EventHandler PackageSourcesChanged;
    }
}
