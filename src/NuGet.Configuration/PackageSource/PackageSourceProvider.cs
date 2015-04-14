using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NuGet.Configuration
{
    public class PackageSourceProvider : IPackageSourceProvider
    {  
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
            : this(settings, providerDefaultPrimarySources, providerDefaultSecondarySources : null , migratePackageSources: null)
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
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            Settings = settings;
            _providerDefaultPrimarySources = providerDefaultPrimarySources ?? Enumerable.Empty<PackageSource>();
            _providerDefaultSecondarySources = providerDefaultSecondarySources ?? Enumerable.Empty<PackageSource>();
            _migratePackageSources = migratePackageSources;
            _configurationDefaultSources = LoadConfigurationDefaultSources();
        }

        private IEnumerable<PackageSource> LoadConfigurationDefaultSources()
        {
#if !DNXCORE50
            var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NuGet");
#else
            var baseDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramData"), "NuGet");
#endif
            var settings = new Settings(baseDirectory, ConfigurationContants.ConfigurationDefaultsFile);

            var sources = new List<PackageSource>();
            IList<SettingValue> disabledPackageSources = settings.GetSettingValues("disabledPackageSources");
            IList<SettingValue> packageSources = settings.GetSettingValues("packageSources");

            foreach (var settingValue in packageSources)
            {
                // In a SettingValue representing a package source, the Key represents the name of the package source and the Value its source
                sources.Add(new PackageSource(settingValue.Value,
                    settingValue.Key,
                    isEnabled: !disabledPackageSources.Any<SettingValue>(p => p.Key.Equals(settingValue.Key, StringComparison.CurrentCultureIgnoreCase)),
                    isOfficial: true));
            }

            return sources;
        }

        /// <summary>
        /// Returns PackageSources if specified in the config file. Else returns the default sources specified in the constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageSource> LoadPackageSources()
        {
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settingsValue = new List<SettingValue>();
            IList<SettingValue> values = Settings.GetSettingValues(ConfigurationContants.PackageSources, isPath: true);
            var machineWideSourcesCount = 0;

            if (values != null && values.Any())
            {
                var machineWideSources = new List<SettingValue>();

                // remove duplicate sources. Pick the one with the highest priority.
                // note that Reverse() is needed because items in 'values' is in
                // ascending priority order.
                foreach (var settingValue in values.Reverse())
                {
                    if (!sources.Contains(settingValue.Key))
                    {
                        if (settingValue.IsMachineWide)
                        {
                            machineWideSources.Add(settingValue);
                        }
                        else
                        {
                            settingsValue.Add(settingValue);
                        }

                        sources.Add(settingValue.Key);
                    }
                }

                // Reverse the the list to be backward compatible
                settingsValue.Reverse();
                machineWideSourcesCount = machineWideSources.Count;

                // Add machine wide sources at the end
                settingsValue.AddRange(machineWideSources);
            }

            var loadedPackageSources = new List<PackageSource>();
            if (settingsValue != null && settingsValue.Any())
            {
                // get list of disabled packages
                var disabledSetting = Settings.GetSettingValues(ConfigurationContants.DisabledPackageSources) ?? Enumerable.Empty<SettingValue>();

                Dictionary<string, SettingValue> disabledSources = new Dictionary<string, SettingValue>(StringComparer.OrdinalIgnoreCase);
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
                loadedPackageSources = settingsValue.
                    Select(p =>
                    {
                        string name = p.Key;
                        string src = p.Value;
                        PackageSourceCredential creds = ReadCredential(name);

                        bool isEnabled = true;
                        SettingValue disabledSource;
                        if (disabledSources.TryGetValue(name, out disabledSource) &&
                            disabledSource.Priority >= p.Priority)
                        {
                            isEnabled = false;
                        }

                        return new PackageSource(src, name, isEnabled)
                        {
                            UserName = creds != null ? creds.Username : null,
                            Password = creds != null ? creds.Password : null,
                            IsPasswordClearText = creds != null && creds.IsPasswordClearText,
                            IsMachineWide = p.IsMachineWide
                        };
                    }).ToList();

                if (_migratePackageSources != null)
                {
                    MigrateSources(loadedPackageSources);
                }
            }

            SetDefaultPackageSources(loadedPackageSources, machineWideSourcesCount);

            foreach (var source in loadedPackageSources)
            {
                source.Description = GetDescription(source);
            }

            return loadedPackageSources;
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
            PackageSourceCredential environmentCredentials = ReadCredentialFromEnvironment(sourceName);

            if (environmentCredentials != null)
            {
                return environmentCredentials;
            }

            var values = Settings.GetNestedValues(ConfigurationContants.CredentialsSectionName, sourceName);
            if (values != null && values.Any())
            {
                string userName = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.UsernameToken, StringComparison.OrdinalIgnoreCase)).Value;

                if (!String.IsNullOrEmpty(userName))
                {
                    string encryptedPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.PasswordToken, StringComparison.OrdinalIgnoreCase)).Value;
                    if (!String.IsNullOrEmpty(encryptedPassword))
                    {
                        return new PackageSourceCredential(userName, EncryptionUtility.DecryptString(encryptedPassword), isPasswordClearText: false);
                    }

                    string clearTextPassword = values.FirstOrDefault(k => k.Key.Equals(ConfigurationContants.ClearTextPasswordToken, StringComparison.Ordinal)).Value;
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
            string rawCredentials = Environment.GetEnvironmentVariable("NuGetPackageSourceCredentials_" + sourceName);
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
            bool hasChanges = false;
            List<PackageSource> packageSourcesToBeRemoved = new List<PackageSource>();

            // doing migration
            for (int i = 0; i < loadedPackageSources.Count; i++)
            {
                PackageSource ps = loadedPackageSources[i];
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

            foreach (PackageSource packageSource in packageSourcesToBeRemoved)
            {
                loadedPackageSources.Remove(packageSource);
            }

            if (hasChanges)
            {
                SavePackageSources(loadedPackageSources);
            }
        }

        private void SetDefaultPackageSources(List<PackageSource> loadedPackageSources, int machineWideSourcesCount)
        {
            var defaultPackageSourcesToBeAdded = new List<PackageSource>();

            if (_configurationDefaultSources == null || !_configurationDefaultSources.Any<PackageSource>())
            {
                // Update provider default sources and use provider default sources since _configurationDefaultSources is empty
                UpdateProviderDefaultSources(loadedPackageSources);
                defaultPackageSourcesToBeAdded = GetPackageSourcesToBeAdded(loadedPackageSources, _providerDefaultPrimarySources, true);
                defaultPackageSourcesToBeAdded = defaultPackageSourcesToBeAdded.Concat(GetPackageSourcesToBeAdded(loadedPackageSources, _providerDefaultSecondarySources, false)).ToList();
            }
            else
            {
                defaultPackageSourcesToBeAdded = GetPackageSourcesToBeAdded(loadedPackageSources, _configurationDefaultSources, false);
            }
            loadedPackageSources.InsertRange(loadedPackageSources.Count - machineWideSourcesCount, defaultPackageSourcesToBeAdded);
        }

        private List<PackageSource> GetPackageSourcesToBeAdded(List<PackageSource> loadedPackageSources, IEnumerable<PackageSource> allDefaultPackageSources, bool checkSecondary)
        {
            // There are 4 different cases to consider for primary/ secondary package sources
            // Case 1. primary/ secondary Package Source is already present matching both feed source and the feed name. Set IsOfficial to true
            // Case 2. primary/ secondary Package Source is already present matching feed source but with a different feed name. DO NOTHING
            // Case 3. primary/ secondary Package Source is not present, but there is another feed source with the same feed name. Override that feed entirely
            // Case 4. primary/ secondary Package Source is not present, simply, add it. In addition, if Primary is getting added 
            // for the first time, promote Primary to Enabled and demote secondary to disabled, if it is already enabled

            var defaultPackageSourcesToBeAdded = new List<PackageSource>();
            foreach (PackageSource packageSource in allDefaultPackageSources)
            {
                int sourceMatchingIndex = loadedPackageSources.FindIndex(p => p.Source.Equals(packageSource.Source, StringComparison.OrdinalIgnoreCase));
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
                    int nameMatchingIndex = loadedPackageSources.FindIndex(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));
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
            bool areProviderDefaultSourcesEnabled = loadedSources.Count == 0 || loadedSources.Where(p => !p.IsMachineWide).Count() == 0
                                                    || loadedSources.Where(p => p.IsEnabled).Count() == 0;

            foreach (PackageSource packageSource in _providerDefaultPrimarySources)
            {
                packageSource.IsEnabled = areProviderDefaultSourcesEnabled;
                packageSource.IsOfficial = true;
            }

            //Mark secondary sources as official but not enable them
            foreach (PackageSource secondaryPackageSource in _providerDefaultSecondarySources)
            {
                secondaryPackageSource.IsEnabled = false;
                secondaryPackageSource.IsOfficial = true;
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            // clear the old values
            Settings.DeleteSection(ConfigurationContants.PackageSources);

            // and write the new ones
            Settings.SetValues(
                ConfigurationContants.PackageSources,
                sources.Where(p => !p.IsMachineWide && p.IsPersistable)
                    .Select(p => new KeyValuePair<string, string>(p.Name, p.Source))
                    .ToList());

            // overwrite new values for the <disabledPackageSources> section
            Settings.DeleteSection(ConfigurationContants.DisabledPackageSources);

            Settings.SetValues(
                ConfigurationContants.DisabledPackageSources,
                sources.Where(p => !p.IsEnabled).Select(p => new KeyValuePair<string, string>(p.Name, "true")).ToList());

            // Overwrite the <packageSourceCredentials> section
            Settings.DeleteSection(ConfigurationContants.CredentialsSectionName);

            var sourceWithCredentials = sources.Where(s => !String.IsNullOrEmpty(s.UserName) && !String.IsNullOrEmpty(s.Password));
            foreach (var source in sourceWithCredentials)
            {
                Settings.SetNestedValues(ConfigurationContants.CredentialsSectionName, source.Name, new[] {
                    new KeyValuePair<string, string>(ConfigurationContants.UsernameToken, source.UserName),
                    ReadPasswordValues(source)
                });
            }

            if (PackageSourcesSaved != null)
            {
                PackageSourcesSaved(this, EventArgs.Empty);
            }
        }

        private static KeyValuePair<string, string> ReadPasswordValues(PackageSource source)
        {
            string passwordToken = source.IsPasswordClearText ? ConfigurationContants.ClearTextPasswordToken : ConfigurationContants.PasswordToken;
            string passwordValue = source.IsPasswordClearText ? source.Password : EncryptionUtility.EncryptString(source.Password);

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

            string value = Settings.GetValue(ConfigurationContants.DisabledPackageSources, source.Name);

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
        /// Saves the <paramref name="source"/> as the active source.
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

        public event EventHandler PackageSourcesSaved;
    }
}
