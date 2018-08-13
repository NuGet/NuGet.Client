// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Concrete implementation of ISettings to support NuGet Settings
    /// Wrapper for computed settings from given settings files
    /// </summary>
    public class Settings : ISettings
    {
        /// <summary>
        /// Default file name for a settings file is 'NuGet.config'
        /// Also, the user level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        public static readonly string DefaultSettingsFileName = "NuGet.Config";

        /// <summary>
        /// NuGet config names with casing ordered by precedence.
        /// </summary>
        public static readonly string[] OrderedSettingsFileNames =
            PathUtility.IsFileSystemCaseInsensitive ?
            new[] { DefaultSettingsFileName } :
            new[]
            {
                "nuget.config", // preferred style
                "NuGet.config", // Alternative
                DefaultSettingsFileName  // NuGet v2 style
            };

        public static readonly string[] SupportedMachineWideConfigExtension =
            RuntimeEnvironmentHelper.IsWindows ?
            new[] { "*.config" } :
            new[] { "*.Config", "*.config" };

        private bool Cleared { get; set; }

        private SettingsFile _settingsHead { get; }

        private Dictionary<string, SettingsSection> _computedSections { get; set; }

        public SettingsSection GetSection(string sectionName)
        {
            if (_computedSections.TryGetValue(sectionName, out var section))
            {
                return section;
            }

            return null;
        }

        public bool CreateSection(SettingsSection section, bool isBatchOperation = false)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (section.IsEmpty() || _computedSections.ContainsKey(section.Name))
            {
                return false;
            }

            var outputSettingsFile = GetOutputSettingsFileForSection(section.Name);

            if (outputSettingsFile != null)
            {
                return outputSettingsFile.CreateSection(section, isBatchOperation);
            }

            return false;
        }

        public bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                throw new ArgumentNullException(nameof(sectionName));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // Check if set is an update
            if (_computedSections.TryGetValue(sectionName, out var section))
            {
                if (section.TryUpdateChildItem(item, isBatchOperation))
                {
                    return true;
                }
            }

            var outputSettingsFile = GetOutputSettingsFileForSection(sectionName);

            // Set is an add
            return outputSettingsFile != null && outputSettingsFile.SetItemInSection(sectionName, item.Copy(), isBatchOperation);
        }

        public event EventHandler SettingsChanged = delegate { };

        public Settings(string root)
            : this(new SettingsFile(root)) { }

        public Settings(string root, string fileName)
            : this(new SettingsFile(root, fileName)) { }

        public Settings(string root, string fileName, bool isMachineWide)
            : this(new SettingsFile(root, fileName, isMachineWide)) { }

        private Settings(SettingsFile settingsHead)
        {
            _settingsHead = settingsHead;

            var curr = _settingsHead;
            while (curr != null)
            {
                curr.SettingsChanged += (_, __) =>
                {
                    MergeSettingFilesValues();
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                };

                curr = curr.Next;
            }

            MergeSettingFilesValues();
        }

        private void MergeSettingFilesValues()
        {
            var computedSections = new Dictionary<string, SettingsSection>();

            var curr = _settingsHead;
            while (curr != null)
            {
                curr.MergeSectionsInto(computedSections);
                curr = curr.Next;
            }

            _computedSections = computedSections;
        }

        private ISettingsFile GetOutputSettingsFileForSection(string sectionName)
        {
            // Search for the furthest from the user that can be written
            // to that is not clearing the ones before it on the hierarchy
            var writteableSettingsFiles = Priority.Where(f => !f.IsMachineWide);

            var clearedSections = writteableSettingsFiles.Select(s => s.GetSection(sectionName)).Where(s => s != null && s.HasClearChild());
            if (clearedSections.Any())
            {
                return clearedSections.Last().Origin;
            }

            // if none have a clear tag, default to furthest from the user
            return writteableSettingsFiles.FirstOrDefault();
        }

        /// <summary>
        /// Enumerates the sequence of <see cref="ISettingsFile"/> instances
        /// used in the order they where read
        /// </summary>
        internal IEnumerable<ISettingsFile> Priority
        {
            get
            {
                // explore the linked list, terminating when a duplicate path is found
                var current = _settingsHead;
                var found = new List<SettingsFile>();
                var paths = new HashSet<string>();
                while (current != null && paths.Add(current.ConfigFilePath))
                {
                    found.Add(current);
                    current = current.Next;
                }

                return found;
            }
        }

        public void Save()
        {
            foreach(var settingsFile in Priority)
            {
                settingsFile.Save();
            }
        }

        /// <summary>
        /// Load default settings based on a directory.
        /// This includes machine wide settings.
        /// </summary>
        public static ISettings LoadDefaultSettings(string root)
        {
            return LoadSettings(
                root,
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting(),
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// Loads user settings from the NuGet configuration files. The method walks the directory
        /// tree in <paramref name="root" /> up to its root, and reads each NuGet.config file
        /// it finds in the directories. It then reads the user specific settings,
        /// which is file <paramref name="configFileName" />
        /// in <paramref name="root" /> if <paramref name="configFileName" /> is not null,
        /// If <paramref name="configFileName" /> is null, the user specific settings file is
        /// %AppData%\NuGet\NuGet.config.
        /// After that, the machine wide settings files are added.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="root" /> is c:\dir1\dir2, <paramref name="configFileName" />
        /// is "userConfig.file", the files loaded are (in the order that they are loaded):
        /// c:\dir1\dir2\nuget.config
        /// c:\dir1\nuget.config
        /// c:\nuget.config
        /// c:\dir1\dir2\userConfig.file
        /// machine wide settings (e.g. c:\programdata\NuGet\Config\*.config)
        /// </remarks>
        /// <param name="root">
        /// The file system to walk to find configuration files.
        /// Can be null.
        /// </param>
        /// <param name="configFileName">The user specified configuration file.</param>
        /// <param name="machineWideSettings">
        /// The machine wide settings. If it's not null, the
        /// settings files in the machine wide settings are added after the user sepcific
        /// config file.
        /// </param>
        /// <returns>The settings object loaded.</returns>
        public static ISettings LoadDefaultSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings)
        {
            return LoadSettings(
                root,
                configFileName,
                machineWideSettings,
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// Loads Specific NuGet.Config file. The method only loads specific config file 
        /// which is file <paramref name="configFileName"/>from <paramref name="root"/>.
        /// </summary>
        public static ISettings LoadSpecificSettings(string root, string configFileName)
        {
            if (string.IsNullOrEmpty(configFileName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(configFileName));
            }

            return LoadSettings(
                root,
                configFileName,
                machineWideSettings: null,
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        public static ISettings LoadSettingsGivenConfigPaths(IList<string> configFilePaths)
        {
            var settings = new List<SettingsFile>();
            if (configFilePaths == null || configFilePaths.Count == 0)
            {
                return NullSettings.Instance;
            }

            foreach (var configFile in configFilePaths)
            {
                var file = new FileInfo(configFile);
                settings.Add(new SettingsFile(file.DirectoryName, file.Name));
            }

            return LoadSettingsForSpecificConfigs(
                settings.First().Root,
                settings.First().FileName,
                validSettingFiles: settings,
                machineWideSettings: null,
                loadUserWideSettings: false,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// For internal use only
        /// </summary>
        internal static ISettings LoadSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings,
            bool loadUserWideSettings,
            bool useTestingGlobalPath)
        {
            // Walk up the tree to find a config file; also look in .nuget subdirectories
            // If a configFile is passed, don't walk up the tree. Only use that single config file.
            var validSettingFiles = new List<SettingsFile>();
            if (root != null && string.IsNullOrEmpty(configFileName))
            {
                validSettingFiles.AddRange(
                    GetSettingsFilesFullPath(root)
                        .Select(f => ReadSettings(root, f))
                        .Where(f => f != null));
            }

            return LoadSettingsForSpecificConfigs(
                root,
                configFileName,
                validSettingFiles,
                machineWideSettings,
                loadUserWideSettings,
                useTestingGlobalPath);
        }

        private static ISettings LoadSettingsForSpecificConfigs(
            string root,
            string configFileName,
            List<SettingsFile> validSettingFiles,
            IMachineWideSettings machineWideSettings,
            bool loadUserWideSettings,
            bool useTestingGlobalPath)
        {
            if (loadUserWideSettings)
            {
                var userSpecific = LoadUserSpecificSettings(root, configFileName, useTestingGlobalPath);
                if (userSpecific != null)
                {
                    validSettingFiles.Add(userSpecific);
                }
            }

            if (machineWideSettings != null && machineWideSettings.Settings is Settings mwSettings && string.IsNullOrEmpty(configFileName))
            {
                // Priority gives you the settings file in the order you want to start reading them,
                // we want them in the reverse order to add them correctly to the settings and connect
                // them to the other configs
                validSettingFiles.AddRange(
                    mwSettings.Priority.Reverse().Select(
                        s => new SettingsFile(s.Root, s.FileName, s.IsMachineWide)));
            }

            if (validSettingFiles?.Any() != true)
            {
                // This means we've failed to load all config files and also failed to load or create the one in %AppData%
                // Work Item 1531: If the config file is malformed and the constructor throws, NuGet fails to load in VS.
                // Returning a null instance prevents us from silently failing and also from picking up the wrong config
                return NullSettings.Instance;
            }

            SettingsFile.ConnectSettingsFilesLinkedList(validSettingFiles);

            // Create a settings object with the linked list head. Typically, it's either the config file in %ProgramData%\NuGet\Config,
            // or the user wide config (%APPDATA%\NuGet\nuget.config) if there are no machine
            // wide config files. The head file is the one we want to read first, while the user wide config
            // is the one that we want to write to.
            // TODO: add UI to allow specifying which one to write to
            return new Settings(validSettingFiles.Last());
        }

        private static SettingsFile LoadUserSpecificSettings(
            string root,
            string configFileName,
            bool useTestingGlobalPath)
        {
            // Path.Combine is performed with root so it should not be null
            // However, it is legal for it be empty in this method
            var rootDirectory = root ?? string.Empty;

            // for the default location, allow case where file does not exist, in which case it'll end
            // up being created if needed
            SettingsFile userSpecificSettings = null;
            if (configFileName == null)
            {
                var defaultSettingsFilePath = string.Empty;
                if (useTestingGlobalPath)
                {
                    defaultSettingsFilePath = Path.Combine(rootDirectory, "TestingGlobalPath", DefaultSettingsFileName);
                }
                else
                {
                    var userSettingsDir = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);

                    // If there is no user settings directory, return no settings
                    if (userSettingsDir == null)
                    {
                        return null;
                    }
                    defaultSettingsFilePath = Path.Combine(userSettingsDir, DefaultSettingsFileName);
                }

                userSpecificSettings = ReadSettings(rootDirectory, defaultSettingsFilePath);

                if (File.Exists(defaultSettingsFilePath) && userSpecificSettings.IsEmpty())
                {
                    var trackFilePath = Path.Combine(Path.GetDirectoryName(defaultSettingsFilePath), NuGetConstants.AddV3TrackFile);

                    if (!File.Exists(trackFilePath))
                    {
                        File.Create(trackFilePath).Dispose();

                        var defaultSource = new SourceItem(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, protocolVersion: "3");

                        var packageSourcesSection = userSpecificSettings.GetSection(ConfigurationConstants.PackageSources);
                        if (packageSourcesSection != null)
                        {
                            packageSourcesSection.AddChild(defaultSource);
                        }
                        else
                        {
                            userSpecificSettings.CreateSection(new SettingsSection(ConfigurationConstants.PackageSources, defaultSource));
                        }
                    }
                }
            }
            else
            {
                if (!FileSystemUtility.DoesFileExistIn(rootDirectory, configFileName))
                {
                    var message = string.Format(CultureInfo.CurrentCulture,
                        Resources.FileDoesNotExist,
                        Path.Combine(rootDirectory, configFileName));
                    throw new InvalidOperationException(message);
                }

                userSpecificSettings = ReadSettings(rootDirectory, configFileName);
            }

            return userSpecificSettings;
        }

        /// <summary>
        /// Loads the machine wide settings.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="paths" /> is {"IDE", "Version", "SKU" }, then
        /// the files loaded are (in the order that they are loaded):
        /// %programdata%\NuGet\Config\IDE\Version\SKU\*.config
        /// %programdata%\NuGet\Config\IDE\Version\*.config
        /// %programdata%\NuGet\Config\IDE\*.config
        /// %programdata%\NuGet\Config\*.config
        /// </remarks>
        /// <param name="root">The file system in which the settings files are read.</param>
        /// <param name="paths">The additional paths under which to look for settings files.</param>
        /// <returns>The list of settings read.</returns>
        public static ISettings LoadMachineWideSettings(
            string root,
            params string[] paths)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("root cannot be null or empty");
            }

            var settingFiles = new List<SettingsFile>();
            var combinedPath = Path.Combine(paths);

            while (true)
            {
                // load setting files in directory
                foreach (var file in FileSystemUtility.GetFilesRelativeToRoot(root, combinedPath, SupportedMachineWideConfigExtension, SearchOption.TopDirectoryOnly))
                {
                    var settings = ReadSettings(root, file, isMachineWideSettings: true);
                    if (settings != null)
                    {
                        settingFiles.Add(settings);
                    }
                }

                if (combinedPath.Length == 0)
                {
                    break;
                }

                var index = combinedPath.LastIndexOf(Path.DirectorySeparatorChar);
                if (index < 0)
                {
                    index = 0;
                }
                combinedPath = combinedPath.Substring(0, index);
            }

            if (settingFiles.Any())
            {
                SettingsFile.ConnectSettingsFilesLinkedList(settingFiles);

                return new Settings(settingFiles.Last());
            }

            return NullSettings.Instance;
        }

        public static string ApplyEnvironmentTransform(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return Environment.ExpandEnvironmentVariables(value);
        }

        public static Tuple<string, string> GetFileNameAndItsRoot(string root, string settingsPath)
        {
            string fileName = null;
            string directory = null;

            if (Path.IsPathRooted(settingsPath))
            {
                fileName = Path.GetFileName(settingsPath);
                directory = Path.GetDirectoryName(settingsPath);
            }
            else if (!FileSystemUtility.IsPathAFile(settingsPath))
            {
                var fullPath = Path.Combine(root ?? string.Empty, settingsPath);
                fileName = Path.GetFileName(fullPath);
                directory = Path.GetDirectoryName(fullPath);
            }
            else
            {
                fileName = settingsPath;
                directory = root;
            }

            return new Tuple<string, string>(fileName, directory);
        }

        internal static string ResolvePath(ISettingsFile originConfig, string path)
        {
            if (Uri.TryCreate(path, UriKind.Relative, out var _) && originConfig != null)
            {
                return ResolveRelativePath(originConfig, path);
            }

            return path;
        }

        private static string ResolveRelativePath(ISettingsFile originConfig, string path)
        {
            if (originConfig == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return Path.Combine(originConfig.Root, ResolvePath(Path.GetDirectoryName(originConfig.ConfigFilePath), path));
        }

        private static string ResolvePath(string configDirectory, string value)
        {
            // Three cases for when Path.IsRooted(value) is true:
            // 1- C:\folder\file
            // 2- \\share\folder\file
            // 3- \folder\file
            // In the first two cases, we want to honor the fully qualified path
            // In the last case, we want to return X:\folder\file with X: drive where config file is located.
            // However, Path.Combine(path1, path2) always returns path2 when Path.IsRooted(path2) == true (which is current case)
            var root = Path.GetPathRoot(value);
            // this corresponds to 3rd case
            if (root != null
                && root.Length == 1
                && (root[0] == Path.DirectorySeparatorChar || value[0] == Path.AltDirectorySeparatorChar))
            {
                return Path.Combine(Path.GetPathRoot(configDirectory), value.Substring(1));
            }
            return Path.Combine(configDirectory, value);
        }

        private static SettingsFile ReadSettings(string settingsRoot, string settingsPath, bool isMachineWideSettings = false)
        {
            try
            {
                var tuple = GetFileNameAndItsRoot(settingsRoot, settingsPath);
                var filename = tuple.Item1;
                var root = tuple.Item2;
                return new SettingsFile(root, filename, isMachineWideSettings);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        /// <remarks>
        /// Order is most significant (e.g. applied last) to least significant (applied first)
        /// ex:
        /// c:\someLocation\nuget.config
        /// c:\nuget.config
        /// </remarks>
        private static IEnumerable<string> GetSettingsFilesFullPath(string root)
        {
            // for dirs obtained by walking up the tree, only consider setting files that already exist.
            // otherwise we'd end up creating them.
            foreach (var dir in GetSettingsFilePaths(root))
            {
                var fileName = GetSettingsFileNameFromDir(dir);
                if (fileName != null)
                {
                    yield return fileName;
                }
            }

            yield break;
        }

        /// <summary>
        /// Checks for each possible casing of nuget.config in the directory. The first match is
        /// returned. If there are no nuget.config files null is returned.
        /// </summary>
        /// <remarks>For windows <see cref="OrderedSettingsFileNames"/> contains a single casing since
        /// the file system is case insensitive.</remarks>
        private static string GetSettingsFileNameFromDir(string directory)
        {
            foreach (var nugetConfigCasing in OrderedSettingsFileNames)
            {
                var file = Path.Combine(directory, nugetConfigCasing);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetSettingsFilePaths(string root)
        {
            while (root != null)
            {
                yield return root;
                root = Path.GetDirectoryName(root);
            }

            yield break;
        }

        // TODO: Delete obsolete methods
#pragma warning disable CS0618 // Type or member is obsolete

        [Obsolete("GetValue(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public string GetValue(string section, string key, bool isPath = false)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            var sectionElement = GetSection(section);
            var item = sectionElement?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, key);

            if (isPath)
            {
                return item?.GetValueAsPath();
            }

            return item?.Value;
        }

        [Obsolete("GetAllSubsections(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<string> GetAllSubsections(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            var sectionElement = GetSection(section);

            if (sectionElement == null)
            {
                return new List<string>().AsReadOnly();
            }

            return sectionElement.Children.Where(c => c is CredentialsItem || c is UnknownItem).Select(i => i.Name).ToList().AsReadOnly();
        }

        [Obsolete("GetSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IList<SettingValue> GetSettingValues(string section, bool isPath = false)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            var sectionElement = GetSection(section);

            if (sectionElement == null)
            {
                return new List<SettingValue>().AsReadOnly();
            }

            return sectionElement?.Children.Select(i =>
            {
                if (i is AddItem addItem)
                {
                    return TransformAddItem(addItem, isPath);
                }

                return null;
            }).Where(i => i != null).ToList().AsReadOnly();
        }

        [Obsolete("GetNestedValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(subSection))
            {
                throw new ArgumentNullException(nameof(subSection));
            }

            var values = GetNestedSettingValues(section, subSection);

            return values.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)).ToList().AsReadOnly();
        }

        [Obsolete("GetNestedSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(subSection))
            {
                throw new ArgumentNullException(nameof(subSection));
            }

            var sectionElement = GetSection(section);
            if (sectionElement == null)
            {
                return new List<SettingValue>().AsReadOnly();
            }

            return sectionElement.Children.SelectMany(i =>
            {
                var settingValues = new List<SettingValue>();

                if (string.Equals(i.Name, subSection, StringComparison.OrdinalIgnoreCase))
                {
                    if (i is CredentialsItem credentials)
                    {
                        settingValues.Add(TransformAddItem(credentials.Username));
                        settingValues.Add(TransformAddItem(credentials.Password));
                    }
                    else if (i is UnknownItem unknown)
                    {
                        if (unknown.Children != null && unknown.Children.Any())
                        {
                            settingValues.AddRange(unknown.Children.Where(c => c is AddItem).Select(item => TransformAddItem(item as AddItem)).ToList());
                        }
                    }
                }

                return settingValues;
            }).ToList().AsReadOnly();
        }

        [Obsolete("SetValue(...) is deprecated, please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        public void SetValue(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            SetItemInSection(section, new AddItem(key, value));
        }

        [Obsolete("SetValues(...) is deprecated, please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        public void SetValues(string section, IReadOnlyList<SettingValue> values)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            foreach (var value in values)
            {
                SetItemInSection(section, TransformSettingValue(value), isBatchOperation: true);
            }

            Save();
        }

        [Obsolete("UpdateSections(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void UpdateSections(string section, IReadOnlyList<SettingValue> values)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            foreach (var value in values)
            {
                SetItemInSection(section, TransformSettingValue(value), isBatchOperation: true);
            }

            Save();
        }

        [Obsolete("UpdateSubsections(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(subsection))
            {
                throw new ArgumentNullException(nameof(subsection));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.Any())
            {
                SettingsItem itemToAdd = null;

                if (string.Equals(ConfigurationConstants.CredentialsSectionName, section, StringComparison.OrdinalIgnoreCase))
                {
                    var username = string.Empty;
                    var password = string.Empty;
                    var isPasswordClearText = true;
                    var validAuthenticationTypes = string.Empty;

                    foreach (var item in values)
                    {
                        if (string.Equals(item.Key, ConfigurationConstants.UsernameToken, StringComparison.OrdinalIgnoreCase))
                        {
                            username = item.Value;
                        }
                        else if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
                        {
                            password = item.Value;
                            isPasswordClearText = false;
                        }
                        else if (string.Equals(item.Key, ConfigurationConstants.ClearTextPasswordToken, StringComparison.OrdinalIgnoreCase))
                        {
                            password = item.Value;
                            isPasswordClearText = true;
                        }
                        else if (string.Equals(item.Key, ConfigurationConstants.ValidAuthenticationTypesToken, StringComparison.OrdinalIgnoreCase))
                        {
                            validAuthenticationTypes = item.Value;
                        }
                    }

                    itemToAdd = new CredentialsItem(subsection, username, password, isPasswordClearText, validAuthenticationTypes);
                }
                else
                {
                    itemToAdd = new UnknownItem(subsection, attributes: null, children: values.Select(v => TransformSettingValue(v)));
                }

                SetItemInSection(section, itemToAdd);
            }
            else
            {
                var sectionElement = GetSection(section);
                var item = sectionElement?.Children.Where(c => string.Equals(c.Name, subsection, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                item?.RemoveFromCollection();
            }
        }

        [Obsolete("SetNestedValues(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(subsection))
            {
                throw new ArgumentNullException(nameof(subsection));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            SetNestedSettingValues(section, subsection, values.Select(kvp => new SettingValue(kvp.Key, kvp.Value, isMachineWide: false)).ToList());
        }

        [Obsolete("SetNestedSettingValues(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(subsection))
            {
                throw new ArgumentNullException(nameof(subsection));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var updatedCurrentElement = false;
            var sectionElement = GetSection(section);
            if (sectionElement != null && values.Any())
            {
                var subsectionItem = sectionElement.Children.FirstOrDefault(c => string.Equals(c.Name, subsection, StringComparison.OrdinalIgnoreCase));

                if (subsectionItem != null && subsectionItem is UnknownItem unknown)
                {
                    foreach(var value in values)
                    {
                        unknown.AddChild(TransformSettingValue(value), isBatchOperation: true);
                    }

                    Save();
                    updatedCurrentElement = true;
                }
            }

            if (!updatedCurrentElement)
            {
                SetItemInSection(section, new UnknownItem(subsection, attributes: null, children: values.Select(v => TransformSettingValue(v))));
            }
        }

        [Obsolete("DeleteValue(...) is deprecated, please interact directly with the SettingsElement you want to delete.")]
        public bool DeleteValue(string section, string key)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            var sectionElement = GetSection(section);
            var item = sectionElement?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, key);

            if (item != null)
            {
                return item.RemoveFromCollection();
            }

            return false;
        }

        [Obsolete("DeleteSection(...) is deprecated, please interact directly with the SettingsElement you want to delete.")]
        public bool DeleteSection(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            var sectionElement = GetSection(section);

            if (sectionElement != null)
            {
                var success = true;

                foreach (var item in sectionElement.Children)
                {
                    success = success && item.RemoveFromCollection(isBatchOperation: true);
                }

                Save();

                return success;
            }

            return false;
        }

        private SettingValue TransformAddItem(AddItem addItem, bool isPath = false)
        {
            var value = isPath ? addItem.GetValueAsPath() : addItem.Value;
            addItem.TryGetAttributeValue(ConfigurationConstants.ValueAttribute, out var originalValue);

            var settingValue = new SettingValue(addItem.Key, value, origin: this, isMachineWide: addItem.Origin?.IsMachineWide ?? false, originalValue: originalValue, priority: 0);

            foreach (var attribute in addItem.Attributes)
            {
                // Add all attributes other than ConfigurationContants.KeyAttribute and ConfigurationContants.ValueAttribute to AdditionalValues
                if (!string.Equals(attribute.Key, ConfigurationConstants.KeyAttribute, StringComparison.Ordinal) &&
                    !string.Equals(attribute.Key, ConfigurationConstants.ValueAttribute, StringComparison.Ordinal))
                {
                    settingValue.AdditionalData[attribute.Key] = attribute.Value;
                }
            }

            return settingValue;
        }

        private AddItem TransformSettingValue(SettingValue value)
        {
            return new AddItem(value.Key, value.Value, value.AdditionalData);
        }

#pragma warning restore CS0618 // Type or member is obsolete
    }
}
