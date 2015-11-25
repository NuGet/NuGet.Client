// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NuGet.Configuration
{
    public class PackageSourceProvider : IPackageSourceProvider
    {
        private const int MaxSupportedProtocolVersion = 3;

        private ISettings Settings { get; set; }
        private readonly IEnumerable<PackageSource> _providerDefaultPrimarySources;
        private readonly IEnumerable<PackageSource> _providerDefaultSecondarySources;
        private readonly IDictionary<PackageSource, PackageSource> _migratePackageSources;
        private readonly IEnumerable<PackageSource> _configurationDefaultSources;

        public PackageSourceProvider(ISettings settings)
            : this(settings, providerDefaultPrimarySources: null, providerDefaultSecondarySources: null)
        {
        }

        /// <summary>
        /// Creates a new PackageSourceProvider instance.
        /// </summary>
        /// <param name="settings">Specifies the settings file to use to read package sources.</param>
        /// <param name="providerDefaultPrimarySources">The primary default sources you would like to use</param>
        public PackageSourceProvider(ISettings settings, IEnumerable<PackageSource> providerDefaultPrimarySources)
            : this(settings, providerDefaultPrimarySources, providerDefaultSecondarySources: null, migratePackageSources: null)
        {
        }

        /// <summary>
        /// Creates a new PackageSourceProvider instance.
        /// </summary>
        /// <param name="settings">Specifies the settings file to use to read package sources.</param>
        /// <param name="providerDefaultPrimarySources">The primary default sources you would like to use</param>
        /// <param name="providerDefaultSecondarySources">The secondary default sources you would like to use</param>
        public PackageSourceProvider(ISettings settings, IEnumerable<PackageSource> providerDefaultPrimarySources, IEnumerable<PackageSource> providerDefaultSecondarySources)
            : this(settings, providerDefaultPrimarySources, providerDefaultSecondarySources, migratePackageSources: null)
        {
        }

        public PackageSourceProvider(
          ISettings settings,
          IEnumerable<PackageSource> providerDefaultPrimarySources,
          IEnumerable<PackageSource> providerDefaultSecondarySources,
          IDictionary<PackageSource, PackageSource> migratePackageSources)
            : this(settings,
                  providerDefaultPrimarySources,
                  providerDefaultSecondarySources,
                  migratePackageSources,
                  ConfigurationDefaults.Instance.DefaultPackageSources)
        {
        }

        public PackageSourceProvider(
            ISettings settings,
            IEnumerable<PackageSource> providerDefaultPrimarySources,
            IEnumerable<PackageSource> providerDefaultSecondarySources,
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
            _providerDefaultPrimarySources = providerDefaultPrimarySources ?? Enumerable.Empty<PackageSource>();
            _providerDefaultSecondarySources = providerDefaultSecondarySources ?? Enumerable.Empty<PackageSource>();
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
            var sourceSettingValues = Settings.GetSettingValues(ConfigurationContants.PackageSources, isPath: true) ??
                                      Enumerable.Empty<SettingValue>();

            // Order the list so that they are ordered in priority order
            var settingValues = sourceSettingValues.OrderByDescending(setting => setting.Priority);

            // get list of disabled packages
            var disabledSetting = Settings.GetSettingValues(ConfigurationContants.DisabledPackageSources) ?? Enumerable.Empty<SettingValue>();

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
                if (disabledSources.TryGetValue(name, out disabledSource)
                    &&
                    disabledSource.Priority >= setting.Priority)
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

            SetDefaultPackageSources(loadedPackageSources);

            foreach (var source in loadedPackageSources)
            {
                source.Description = GetDescription(source);
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
                packageSource.Password = credentials.Password;
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
            if (setting.AdditionalData.TryGetValue(ConfigurationContants.ProtocolVersionAttribute, out protocolVersionString)
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

        // Gets the description of the source if it matches a default source.
        // Returns null if it does not match a default source
        private string GetDescription(PackageSource source)
        {
            var matchingSource = _providerDefaultPrimarySources.FirstOrDefault(
                s => StringComparer.OrdinalIgnoreCase.Equals(s.Source, source.Source));
            if (matchingSource != null)
            {
                return matchingSource.Description;
            }

            return null;
        }

        private PackageSourceCredential ReadCredential(string sourceName)
        {
            var environmentCredentials = ReadCredentialFromEnvironment(sourceName);

            if (environmentCredentials != null)
            {
                return environmentCredentials;
            }

            var values = Settings.GetNestedValues(ConfigurationContants.CredentialsSectionName, sourceName);
            if (values != null
                && values.Any())
            {
                var userName = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.UsernameToken, StringComparison.OrdinalIgnoreCase)).Value;

                if (!String.IsNullOrEmpty(userName))
                {
                    var encryptedPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.PasswordToken, StringComparison.OrdinalIgnoreCase)).Value;
                    if (!String.IsNullOrEmpty(encryptedPassword))
                    {
                        return new PackageSourceCredential(userName, EncryptionUtility.DecryptString(encryptedPassword), isPasswordClearText: false);
                    }

                    var clearTextPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.ClearTextPasswordToken, StringComparison.Ordinal)).Value;
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

        private void SetDefaultPackageSources(List<PackageSource> loadedPackageSources)
        {
            var defaultPackageSourcesToBeAdded = new List<PackageSource>();

            if (_configurationDefaultSources == null
                || !_configurationDefaultSources.Any<PackageSource>())
            {
                // Update provider default sources and use provider default sources since _configurationDefaultSources is empty
                UpdateProviderDefaultSources(loadedPackageSources);
                defaultPackageSourcesToBeAdded = GetPackageSourcesToBeAdded(
                    loadedPackageSources,
                    Enumerable.Concat(_providerDefaultPrimarySources, _providerDefaultSecondarySources));
            }
            else
            {
                defaultPackageSourcesToBeAdded = GetPackageSourcesToBeAdded(loadedPackageSources, _configurationDefaultSources);
            }

            var defaultSourcesInsertIndex = loadedPackageSources.FindIndex(source => source.IsMachineWide);
            if (defaultSourcesInsertIndex == -1)
            {
                defaultSourcesInsertIndex = loadedPackageSources.Count;
            }

            // Default package sources go ahead of machine wide sources
            loadedPackageSources.InsertRange(defaultSourcesInsertIndex, defaultPackageSourcesToBeAdded);
        }

        private List<PackageSource> GetPackageSourcesToBeAdded(List<PackageSource> loadedPackageSources, IEnumerable<PackageSource> allDefaultPackageSources)
        {
            // There are 4 different cases to consider for primary/ secondary package sources
            // Case 1. primary/ secondary Package Source is already present matching both feed source and the feed name. Set IsOfficial to true
            // Case 2. primary/ secondary Package Source is already present matching feed source but with a different feed name. DO NOTHING
            // Case 3. primary/ secondary Package Source is not present, but there is another feed source with the same feed name. Override that feed entirely
            // Case 4. primary/ secondary Package Source is not present, simply, add it. In addition, if Primary is getting added
            // for the first time, promote Primary to Enabled and demote secondary to disabled, if it is already enabled

            var defaultPackageSourcesToBeAdded = new List<PackageSource>();
            foreach (var packageSource in allDefaultPackageSources)
            {
                var existingIndex = defaultPackageSourcesToBeAdded.FindIndex(
                    source => string.Equals(source.Name, packageSource.Name, StringComparison.OrdinalIgnoreCase));

                // Ignore sources with the same name but lower protocol versions that are already added.
                if (existingIndex != -1)
                {
                    var existingSource = defaultPackageSourcesToBeAdded[existingIndex];
                    if (existingSource.ProtocolVersion < packageSource.ProtocolVersion)
                    {
                        defaultPackageSourcesToBeAdded.RemoveAt(existingIndex);
                        defaultPackageSourcesToBeAdded.Insert(existingIndex, packageSource);
                    }
                    continue;
                }

                var sourceMatchingIndex = loadedPackageSources.FindIndex(p => p.Source.Equals(packageSource.Source, StringComparison.OrdinalIgnoreCase));
                if (sourceMatchingIndex != -1)
                {
                    if (loadedPackageSources[sourceMatchingIndex].Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // Case 1: Both the feed name and source matches. DO NOTHING except set IsOfficial to true
                        loadedPackageSources[sourceMatchingIndex].IsOfficial = true;
                    }
                    else
                    {
                        // Case 2: Only feed source matches but name is different. DO NOTHING
                    }
                }
                else
                {
                    var nameMatchingIndex = loadedPackageSources.FindIndex(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (nameMatchingIndex != -1)
                    {
                        // Case 3: Only feed name matches but source is different. Override it entirely
                        //DO NOTHING
                    }
                    else
                    {
                        // Case 4: Default package source is not present. Add it to the temp list. Later, the temp listed is inserted above the machine wide sources
                        defaultPackageSourcesToBeAdded.Add(packageSource);
                        packageSource.IsOfficial = true;
                    }
                }
            }
            return defaultPackageSourcesToBeAdded;
        }

        private void UpdateProviderDefaultSources(List<PackageSource> loadedSources)
        {
            // If there are NO other non-machine wide sources, providerDefaultPrimarySource should be enabled
            var areProviderDefaultSourcesEnabled = loadedSources.Count == 0 || loadedSources.Where(p => !p.IsMachineWide).Count() == 0
                                                   || loadedSources.Where(p => p.IsEnabled).Count() == 0;

            foreach (var packageSource in _providerDefaultPrimarySources)
            {
                packageSource.IsEnabled = areProviderDefaultSourcesEnabled;
                packageSource.IsOfficial = true;
            }

            //Mark secondary sources as official but not enable them
            foreach (var secondaryPackageSource in _providerDefaultSecondarySources)
            {
                secondaryPackageSource.IsEnabled = areProviderDefaultSourcesEnabled;
                secondaryPackageSource.IsOfficial = true;
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            // clear the old values
            // and write the new ones
            var sourcesToWrite = sources.Where(s => !s.IsMachineWide && s.IsPersistable);

            var existingSettings = (Settings.GetSettingValues(ConfigurationContants.PackageSources, isPath: true) ??
                                    Enumerable.Empty<SettingValue>()).Where(setting => !setting.IsMachineWide).ToList();
            var minPriority = 0;

            // get lowest priority in existingSetting
            if (existingSettings.Count > 0)
            {
                minPriority = existingSettings.Min(setting => setting.Priority);
            }

            existingSettings.RemoveAll(setting => !sources.Any(s => string.Equals(s.Name, setting.Key, StringComparison.OrdinalIgnoreCase)));
            var existingSettingsLookup = existingSettings.ToLookup(setting => setting.Key, StringComparer.OrdinalIgnoreCase);
            var existingDisabledSources = Settings.GetSettingValues(ConfigurationContants.DisabledPackageSources) ??
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
                        settingValue.AdditionalData[ConfigurationContants.ProtocolVersionAttribute] =
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
            Settings.UpdateSections(ConfigurationContants.PackageSources, sourceSettings);

            // overwrite new values for the <disabledPackageSources> section
            Settings.UpdateSections(ConfigurationContants.DisabledPackageSources, sourcesToDisable);

            // Overwrite the <packageSourceCredentials> section
            Settings.DeleteSection(ConfigurationContants.CredentialsSectionName);

            var sourceWithCredentials = sources.Where(s => !String.IsNullOrEmpty(s.UserName) && !String.IsNullOrEmpty(s.Password));
            foreach (var source in sourceWithCredentials)
            {
                Settings.SetNestedValues(ConfigurationContants.CredentialsSectionName, source.Name, new[]
                    {
                        new KeyValuePair<string, string>(ConfigurationContants.UsernameToken, source.UserName),
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
            var passwordToken = source.IsPasswordClearText ? ConfigurationContants.ClearTextPasswordToken : ConfigurationContants.PasswordToken;
            var passwordValue = source.IsPasswordClearText ? source.Password : EncryptionUtility.EncryptString(source.Password);

            return new KeyValuePair<string, string>(passwordToken, passwordValue);
        }

        public void DisablePackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            Settings.SetValue(ConfigurationContants.DisabledPackageSources, source.Name, "true");
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var value = Settings.GetValue(ConfigurationContants.DisabledPackageSources, source.Name);

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
                var activeSource = Settings.GetSettingValues(ConfigurationContants.ActivePackageSourceSectionName).FirstOrDefault();
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
                Settings.DeleteSection(ConfigurationContants.ActivePackageSourceSectionName);
                Settings.SetValue(ConfigurationContants.ActivePackageSourceSectionName, source.Name, source.Source);
            }
            catch (Exception)
            {
                // we want to ignore all errors here.
            }
        }

        private class PackageSourceCredential
        {
            public string Username { get; private set; }

            public string Password { get; private set; }

            public bool IsPasswordClearText { get; private set; }

            public PackageSourceCredential(string username, string password, bool isPasswordClearText)
            {
                Username = username;
                Password = password;
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
