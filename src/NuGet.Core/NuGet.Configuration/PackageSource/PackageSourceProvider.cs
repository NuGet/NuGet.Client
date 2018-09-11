// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.Configuration
{
    public class PackageSourceProvider : IPackageSourceProvider
    {
        public ISettings Settings { get; private set; }

        private const int MaxSupportedProtocolVersion = 3;
        private readonly IEnumerable<PackageSource> _configurationDefaultSources;

        public PackageSourceProvider(
          ISettings settings)
            : this(settings, ConfigurationDefaults.Instance.DefaultPackageSources)
        {
        }

        public PackageSourceProvider(
            ISettings settings,
            IEnumerable<PackageSource> configurationDefaultSources
            )
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.SettingsChanged += (_, __) => { OnPackageSourcesChanged(); };
            _configurationDefaultSources = LoadConfigurationDefaultSources(configurationDefaultSources);
        }

        private IEnumerable<PackageSource> LoadConfigurationDefaultSources(IEnumerable<PackageSource> configurationDefaultSources)
        {
#if !IS_CORECLR
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
                packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource, packageSource.Name);
            }

            return packageSourceLookup.Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource);
        }

        private Dictionary<string, IndexedPackageSource> LoadPackageSourceLookup(bool byName)
        {
            var packageSourcesSection = Settings.GetSection(ConfigurationConstants.PackageSources);
            var sources = packageSourcesSection?.Items.Select(s => s as SourceItem)
                .Where(s => s != null);

            // get list of disabled packages
            var disabledSourcesSection = Settings.GetSection(ConfigurationConstants.DisabledPackageSources);
            var disabledSourcesSettings = disabledSourcesSection?.Items.Select(s => s as AddItem)
                .Where(s => s != null);

            var disabledSources = new HashSet<string>(disabledSourcesSettings?.GroupBy(setting => setting.Key).Select(group => group.First().Key) ?? new List<string>());
            var packageSourceLookup = new Dictionary<string, IndexedPackageSource>(StringComparer.OrdinalIgnoreCase);

            if (sources != null)
            {
                var packageIndex = 0;

                foreach (var setting in sources)
                {
                    var name = setting.Key;
                    var isEnabled = !disabledSources.Contains(name);
                    var packageSource = ReadPackageSource(setting, isEnabled);

                    packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource, byName ? packageSource.Name : packageSource.Source);
                }
            }
            return packageSourceLookup;
        }

        private Dictionary<string, IndexedPackageSource> LoadPackageSourceLookupByName()
        {
            return LoadPackageSourceLookup(byName: true);
        }

        private Dictionary<string, IndexedPackageSource> LoadPackageSourceLookupBySource()
        {
            return LoadPackageSourceLookup(byName: false);
        }

        /// <summary>
        /// Returns PackageSources if specified in the config file. Else returns the default sources specified in the
        /// constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageSource> LoadPackageSources()
        {
            var loadedPackageSources = LoadPackageSourceLookupByName().Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource)
                .ToList();

            if (_configurationDefaultSources != null && _configurationDefaultSources.Any())
            {
                SetDefaultPackageSources(loadedPackageSources);
            }

            return loadedPackageSources;
        }

        private void SetDefaultPackageSources(List<PackageSource> loadedPackageSources)
        {
            var defaultPackageSourcesToBeAdded = new List<PackageSource>();

            foreach (var packageSource in _configurationDefaultSources)
            {
                var sourceMatching = loadedPackageSources.Any(p => p.Source.Equals(packageSource.Source, StringComparison.CurrentCultureIgnoreCase));
                var feedNameMatching = loadedPackageSources.Any(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));

                if (!sourceMatching && !feedNameMatching)
                {
                    defaultPackageSourcesToBeAdded.Add(packageSource);
                }
            }

            var defaultSourcesInsertIndex = loadedPackageSources.FindIndex(source => source.IsMachineWide);

            if (defaultSourcesInsertIndex == -1)
            {
                defaultSourcesInsertIndex = loadedPackageSources.Count;
            }

            loadedPackageSources.InsertRange(defaultSourcesInsertIndex, defaultPackageSourcesToBeAdded);
        }

        private PackageSource ReadPackageSource(SourceItem setting, bool isEnabled)
        {
            var name = setting.Key;
            var packageSource = new PackageSource(setting.GetValueAsPath(), name, isEnabled)
            {
                IsMachineWide = setting.Origin?.IsMachineWide ?? false,
                MaxHttpRequestsPerSource = SettingsUtility.GetMaxHttpRequest(Settings)
            };

            var credentials = ReadCredential(name);
            if (credentials != null)
            {
                packageSource.Credentials = credentials;
            }

            packageSource.ProtocolVersion = ReadProtocolVersion(setting);

            return packageSource;
        }

        private static int ReadProtocolVersion(SourceItem setting)
        {
            if (int.TryParse(setting.ProtocolVersion, out var protocolVersion))
            {
                return protocolVersion;
            }

            return PackageSource.DefaultProtocolVersion;
        }

        private static int AddOrUpdateIndexedSource(
            Dictionary<string, IndexedPackageSource> packageSourceLookup,
            int packageIndex,
            PackageSource packageSource,
            string lookupKey)
        {
            if (!packageSourceLookup.TryGetValue(lookupKey, out var previouslyAddedSource))
            {
                packageSourceLookup[lookupKey] = new IndexedPackageSource
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

            var credentialsSection = Settings.GetSection(ConfigurationConstants.CredentialsSectionName);
            var credentialsItem = credentialsSection?.Items.Select(c => c as CredentialsItem).Where(s => s != null).FirstOrDefault(s => string.Equals(s.Name, sourceName, StringComparison.Ordinal));

            if (credentialsItem != null && !credentialsItem.IsEmpty())
            {
                var username = credentialsItem.Username;
                var password = credentialsItem.Password;
                var authenticationTypes = credentialsItem.ValidAuthenticationTypes;

                return new PackageSourceCredential(sourceName, username, password, credentialsItem.IsPasswordClearText, authenticationTypes);
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

            var match = Regex.Match(rawCredentials.Trim(), @"^Username=(?<user>.*?);\s*Password=(?<pass>.*?)(?:;ValidAuthenticationTypes=(?<authTypes>.*?))?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return new PackageSourceCredential(
                sourceName,
                match.Groups["user"].Value,
                match.Groups["pass"].Value,
                isPasswordClearText: true,
                validAuthenticationTypesText: match.Groups["authTypes"].Value);
        }

        private PackageSource GetPackageSource(string key, Dictionary<string, IndexedPackageSource> sourcesLookup)
        {
            if (sourcesLookup.TryGetValue(key, out var indexedPackageSource))
            {
                return indexedPackageSource.PackageSource;
            }

            if (_configurationDefaultSources != null && _configurationDefaultSources.Any())
            {
                var loadedPackageSources = sourcesLookup.Values
                    .OrderBy(source => source.Index)
                    .Select(source => source.PackageSource)
                    .ToList();

                foreach (var packageSource in _configurationDefaultSources)
                {
                    var sourceMatching = loadedPackageSources.Any(p => p.Source.Equals(packageSource.Source, StringComparison.CurrentCultureIgnoreCase));
                    var feedNameMatching = loadedPackageSources.Any(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));

                    if (sourceMatching || feedNameMatching)
                    {
                        return packageSource;
                    }
                }
            }

            return null;
        }

        public PackageSource GetPackageSourceByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            return GetPackageSource(name, LoadPackageSourceLookupByName());
        }

        public PackageSource GetPackageSourceBySource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            return GetPackageSource(source, LoadPackageSourceLookupBySource());
        }

        public void RemovePackageSource(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var isDirty = false;
            RemovePackageSource(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        private void RemovePackageSource(string name, bool shouldSkipSave, ref bool isDirty)
        {
            // get list of sources
            var packageSourcesSection = Settings.GetSection(ConfigurationConstants.PackageSources);
            var sourcesSettings = packageSourcesSection?.Items.Select(s => s as SourceItem)
                .Where(s => s != null);

            // get list of credentials for sources
            var sourceCredentialsSection = Settings.GetSection(ConfigurationConstants.CredentialsSectionName);
            var sourceCredentialsSettings = sourceCredentialsSection?.Items.Select(s => s as CredentialsItem)
                .Where(s => s != null);

            var sourcesToRemove = sourcesSettings?.Where(s => string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase));
            var credentialsToRemove = sourceCredentialsSettings?.Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (sourcesToRemove != null)
            {
                foreach (var source in sourcesToRemove)
                {
                    try
                    {
                        Settings.Remove(ConfigurationConstants.PackageSources, source);
                        isDirty = true;
                    }
                    catch { }
                }
            }

            RemoveDisabledSource(name, shouldSkipSave: true, isDirty: ref isDirty);

            if (credentialsToRemove != null)
            {
                foreach (var credentials in credentialsToRemove)
                {
                    try
                    {
                        Settings.Remove(ConfigurationConstants.CredentialsSectionName, credentials);
                        isDirty = true;
                    }
                    catch { }
                }
            }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageSourcesChanged();
                isDirty = false;
            }
        }

        [Obsolete("DisablePackageSource(PackageSource source) is deprecated, please use DisablePackageSource(string name) instead.")]
        public void DisablePackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var isDirty = false;
            AddDisabledSource(source.Name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        public void DisablePackageSource(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var isDirty = false;
            AddDisabledSource(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        private void AddDisabledSource(string name, bool shouldSkipSave, ref bool isDirty)
        {
            var settingsLookup = GetExistingSettingsLookup();
            var addedInSameFileAsCurrentSource = false;

            if (settingsLookup.TryGetValue(name, out var sourceSetting))
            {
                try
                {
                    (Settings as Settings).AddOrUpdate(sourceSetting.Origin, ConfigurationConstants.DisabledPackageSources, new AddItem(name, "true"));
                    isDirty = true;
                    addedInSameFileAsCurrentSource = true;
                }
                // We ignore any errors since this means the current source file could not be edited
                catch { }
            }

            if (!addedInSameFileAsCurrentSource)
            {
                Settings.AddOrUpdate(ConfigurationConstants.DisabledPackageSources, new AddItem(name, "true"));
                isDirty = true;
            }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageSourcesChanged();
                isDirty = false;
            }
        }

        public void EnablePackageSource(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var isDirty = false;
            RemoveDisabledSource(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        private void RemoveDisabledSource(string name, bool shouldSkipSave, ref bool isDirty)
        {
            // get list of disabled sources
            var disabledSourcesSection = Settings.GetSection(ConfigurationConstants.DisabledPackageSources);
            var disabledSourcesSettings = disabledSourcesSection?.Items.Select(s => s as AddItem)
                .Where(s => s != null);

            var disableSourcesToRemove = disabledSourcesSettings?.Where(s => string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase));

            if (disableSourcesToRemove != null)
            {
                foreach (var disabledSource in disableSourcesToRemove)
                {
                    Settings.Remove(ConfigurationConstants.DisabledPackageSources, disabledSource);
                    isDirty = true;
                }
            }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageSourcesChanged();
                isDirty = false;
            }
        }

        public void UpdatePackageSource(PackageSource source, bool updateCredentials, bool updateEnabled)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var packageSources = GetExistingSettingsLookup();
            packageSources.TryGetValue(source.Name, out var sourceToUpdate);

            if (sourceToUpdate != null)
            {
                AddItem disabledSourceItem = null;
                CredentialsItem credentialsSettingsItem = null;

                if (updateEnabled)
                {
                    // get list of disabled packages
                    var disabledSourcesSection = Settings.GetSection(ConfigurationConstants.DisabledPackageSources);
                    disabledSourceItem = disabledSourcesSection?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, sourceToUpdate.Name);
                }

                if (updateCredentials)
                {
                    // get list of credentials for sources
                    var credentialsSection = Settings.GetSection(ConfigurationConstants.CredentialsSectionName);
                    credentialsSettingsItem = credentialsSection?.Items.Select(s => s as CredentialsItem)
                        .Where(s => s != null).Where(s => string.Equals(s.Name, sourceToUpdate.Key, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                }

                var oldPackageSource = ReadPackageSource(sourceToUpdate, disabledSourceItem == null);
                var isDirty = false;

                UpdatePackageSource(
                    source,
                    oldPackageSource,
                    disabledSourceItem,
                    credentialsSettingsItem,
                    updateEnabled,
                    updateCredentials,
                    shouldSkipSave: false,
                    isDirty: ref isDirty);
            }
        }

        private void UpdatePackageSource(
            PackageSource newSource,
            PackageSource existingSource,
            AddItem existingDisabledSourceItem, 
            CredentialsItem existingCredentialsItem,
            bool updateEnabled,
            bool updateCredentials,
            bool shouldSkipSave,
            ref bool isDirty)
        {
            if (string.Equals(newSource.Name, existingSource.Name, StringComparison.OrdinalIgnoreCase))
            {
                if ((!string.Equals(newSource.Source, existingSource.Source, StringComparison.OrdinalIgnoreCase) ||
                    newSource.ProtocolVersion != existingSource.ProtocolVersion) && newSource.IsPersistable)
                {
                    Settings.AddOrUpdate(ConfigurationConstants.PackageSources, newSource.AsSourceItem());
                    isDirty = true;
                }

                if (updateEnabled)
                {
                    if (newSource.IsEnabled && existingDisabledSourceItem != null)
                    {
                        Settings.Remove(ConfigurationConstants.DisabledPackageSources, existingDisabledSourceItem);
                        isDirty = true;
                    }

                    if (!newSource.IsEnabled && existingDisabledSourceItem == null)
                    {
                        AddDisabledSource(newSource.Name, shouldSkipSave: true, isDirty: ref isDirty);
                    }
                }

                if (updateCredentials && newSource.Credentials != existingSource.Credentials)
                {
                    if (existingCredentialsItem != null)
                    {
                        if (newSource.Credentials == null)
                        {
                            Settings.Remove(ConfigurationConstants.CredentialsSectionName, existingCredentialsItem);
                            isDirty = true;
                        }
                        else
                        {
                            Settings.AddOrUpdate(ConfigurationConstants.CredentialsSectionName, newSource.Credentials.AsCredentialsItem());
                            isDirty = true;
                        }
                    }
                    else if (newSource.Credentials != null && newSource.Credentials.IsValid())
                    {
                        Settings.AddOrUpdate(ConfigurationConstants.CredentialsSectionName, newSource.Credentials.AsCredentialsItem());
                        isDirty = true;
                    }
                }

                if (!shouldSkipSave && isDirty)
                {
                    Settings.SaveToDisk();
                    OnPackageSourcesChanged();
                    isDirty = false;
                }
            }
        }

        public void AddPackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var isDirty = false;
            AddPackageSource(source, shouldSkipSave: false, isDirty: ref isDirty);
        }

        private void AddPackageSource(PackageSource source, bool shouldSkipSave, ref bool isDirty)
        {
            if (source.IsPersistable)
            {
                Settings.AddOrUpdate(ConfigurationConstants.PackageSources, source.AsSourceItem());
                isDirty = true;
            }

            if (source.IsEnabled)
            {
                RemoveDisabledSource(source.Name, shouldSkipSave: true, isDirty: ref isDirty);
            }
            else
            {
                AddDisabledSource(source.Name, shouldSkipSave: true, isDirty: ref isDirty);
            }

            if (source.Credentials != null && source.Credentials.IsValid())
            {
                Settings.AddOrUpdate(ConfigurationConstants.CredentialsSectionName, source.Credentials.AsCredentialsItem());
                isDirty = true;
            }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageSourcesChanged();
                isDirty = false;
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var isDirty = false;
            var existingSettingsLookup = GetExistingSettingsLookup();

            var disabledSourcesSection = Settings.GetSection(ConfigurationConstants.DisabledPackageSources);
            var existingDisabledSources = disabledSourcesSection?.Items.Select(c => c as AddItem).Where(c => c != null);
            var existingDisabledSourcesLookup = existingDisabledSources?.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

            var credentialsSection = Settings.GetSection(ConfigurationConstants.CredentialsSectionName);
            var existingCredentials = credentialsSection?.Items.Select(c => c as CredentialsItem).Where(c => c != null);
            var existingCredentialsLookup = existingCredentials?.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var source in sources)
            {
                AddItem existingDisabledSourceItem = null;
                SourceItem existingSourceItem = null;
                CredentialsItem existingCredentialsItem = null;

                var existingSourceIsEnabled = existingDisabledSourcesLookup == null || existingDisabledSourcesLookup.TryGetValue(source.Name, out existingDisabledSourceItem);

                if (existingSettingsLookup != null &&
                    existingSettingsLookup.TryGetValue(source.Name, out existingSourceItem) &&
                    ReadProtocolVersion(existingSourceItem) == source.ProtocolVersion)
                {
                    var oldPackageSource = ReadPackageSource(existingSourceItem, existingSourceIsEnabled);

                    existingCredentialsLookup?.TryGetValue(source.Name, out existingCredentialsItem);

                    UpdatePackageSource(
                        source,
                        oldPackageSource,
                        existingDisabledSourceItem,
                        existingCredentialsItem,
                        updateEnabled: true,
                        updateCredentials: true,
                        shouldSkipSave: true,
                        isDirty: ref isDirty);
                }
                else
                {
                    AddPackageSource(source, shouldSkipSave: true, isDirty: ref isDirty);
                }

                if (existingSourceItem != null)
                {
                    existingSettingsLookup.Remove(source.Name);
                }
            }

            if (existingSettingsLookup != null)
            {
                // get list of credentials for sources
                var sourceCredentialsSection = Settings.GetSection(ConfigurationConstants.CredentialsSectionName);
                var sourceCredentialsSettings = sourceCredentialsSection?.Items.Select(s => s as CredentialsItem)
                    .Where(s => s != null);
                var existingsourceCredentialsLookup = sourceCredentialsSettings?.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var sourceItem in existingSettingsLookup)
                {
                    if (existingDisabledSourcesLookup != null && existingDisabledSourcesLookup.TryGetValue(sourceItem.Value.Key, out var existingDisabledSourceItem))
                    {
                        Settings.Remove(ConfigurationConstants.DisabledPackageSources, existingDisabledSourceItem);
                        isDirty = true;
                    }

                    if (existingsourceCredentialsLookup != null && existingsourceCredentialsLookup.TryGetValue(sourceItem.Value.Key, out var existingSourceCredentialItem))
                    {
                        Settings.Remove(ConfigurationConstants.CredentialsSectionName, existingSourceCredentialItem);
                        isDirty = true;
                    }

                    Settings.Remove(ConfigurationConstants.PackageSources, sourceItem.Value);
                    isDirty = true;
                }
            }


            if (isDirty)
            {
                Settings.SaveToDisk();
                OnPackageSourcesChanged();
                isDirty = false;
            }
        }

        private Dictionary<string, SourceItem> GetExistingSettingsLookup()
        {
            var sourcesSection = Settings.GetSection(ConfigurationConstants.PackageSources);
            var existingSettings = sourcesSection?.Items.OfType<SourceItem>().Where(c => !c.Origin.IsMachineWide).ToList();

            var existingSettingsLookup = new Dictionary<string, SourceItem>(StringComparer.OrdinalIgnoreCase);
            if (existingSettings != null)
            {
                foreach (var setting in existingSettings)
                {
                    if (existingSettingsLookup.TryGetValue(setting.Key, out var previouslyAddedSetting) &&
                        ReadProtocolVersion(previouslyAddedSetting) < ReadProtocolVersion(setting) &&
                        ReadProtocolVersion(setting) <= MaxSupportedProtocolVersion)
                    {
                        existingSettingsLookup.Remove(setting.Key);
                    }

                    existingSettingsLookup[setting.Key] = setting;
                }
            }

            return existingSettingsLookup;
        }

        /// <summary>
        /// Fires event PackageSourcesChanged
        /// </summary>
        private void OnPackageSourcesChanged()
        {
            PackageSourcesChanged?.Invoke(this, EventArgs.Empty);
        }

        public string DefaultPushSource
        {
            get
            {
                var source = SettingsUtility.GetDefaultPushSource(Settings);

                if (string.IsNullOrEmpty(source))
                {
                    source = ConfigurationDefaults.Instance.DefaultPushSource;
                }

                return source;
            }
        }

        public bool IsPackageSourceEnabled(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var disabledSources = Settings.GetSection(ConfigurationConstants.DisabledPackageSources);
            var value = disabledSources?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, name);

            // It doesn't matter what value it is.
            // As long as the package source name is persisted in the <disabledPackageSources> section, the source is disabled.
            return value == null;
        }

        [Obsolete("IsPackageSourceEnabled(PackageSource source) is deprecated, please use IsPackageSourceEnabled(string name) instead.")]
        public bool IsPackageSourceEnabled(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return IsPackageSourceEnabled(source.Name);
        }

        /// <summary>
        /// Gets the name of the ActivePackageSource from NuGet.Config
        /// </summary>
        public string ActivePackageSourceName
        {
            get
            {
                var activeSourceSection = Settings.GetSection(ConfigurationConstants.ActivePackageSourceSectionName);
                return activeSourceSection?.Items.Select(c => c as AddItem).Where(c => c != null).FirstOrDefault()?.Key;
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
                var activePackageSourceSection = Settings.GetSection(ConfigurationConstants.ActivePackageSourceSectionName);

                if (activePackageSourceSection != null)
                {
                    foreach(var activePackageSource in activePackageSourceSection.Items)
                    {
                        Settings.Remove(ConfigurationConstants.ActivePackageSourceSectionName, activePackageSource);
                    }
                }

                Settings.AddOrUpdate(ConfigurationConstants.ActivePackageSourceSectionName,
                        new AddItem(source.Name, source.Source));

                Settings.SaveToDisk();
            }
            catch (Exception)
            {
                // we want to ignore all errors here.
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
