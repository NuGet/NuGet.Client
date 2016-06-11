// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Concrete implementation of ISettings to support NuGet Settings
    /// </summary>
    public class Settings : ISettings
    {
        /// <summary>
        /// Default file name for a settings file is 'NuGet.config'
        /// Also, the machine level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        public static readonly string DefaultSettingsFileName = "NuGet.Config";

        /// <summary>
        /// NuGet config names with casing ordered by precedence.
        /// </summary>
        public static readonly string[] OrderedSettingsFileNames =
            (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsMacOSX) ?
            new[] { DefaultSettingsFileName } :
            new[]
            {
                "nuget.config", // preferred style
                "NuGet.config", // Alternative
                DefaultSettingsFileName  // NuGet v2 style
            };

        public static readonly string[] SupportedMachineWideConfigExtension =
            RuntimeEnvironmentHelper.IsWindows?
            new[] { "*.config" } :
            new[] { "*.Config", "*.config" };

        private XDocument ConfigXDocument { get; }
        public string FileName { get; }
        private bool IsMachineWideSettings { get; }
        // next config file to read if any
        private Settings _next;
        // The priority of this setting file
        private int _priority;

        private bool Cleared { get; set; }

        public Settings(string root /*, ILogger logger */)
            : this(root, DefaultSettingsFileName, false)
        {
        }

        public Settings(string root, string fileName)
            : this(root, fileName, false)
        {
        }

        public Settings(string root, string fileName, bool isMachineWideSettings)
        {
            if (String.IsNullOrEmpty(root))
            {
                throw new ArgumentException("root cannot be null or empty", nameof(root));
            }

            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(fileName));
            }

            if (!FileSystemUtility.IsPathAFile(fileName))
            {
                throw new ArgumentException(Resources.Settings_FileName_Cannot_Be_A_Path, nameof(fileName));
            }

            Root = root;
            FileName = fileName;
            XDocument config = null;
            ExecuteSynchronized(() => config = XmlUtility.GetOrCreateDocument(CreateDefaultConfig(), ConfigFilePath));
            ConfigXDocument = config;
            IsMachineWideSettings = isMachineWideSettings;
            CheckConfigRoot();
        }

        public event EventHandler SettingsChanged = delegate { };

        public IEnumerable<ISettings> Priority
        {
            get
            {
                // explore the linked list, terminating when a duplicate path is found
                var current = this;
                var found = new List<Settings>();
                var paths = new HashSet<string>();
                while (current != null && paths.Add(current.ConfigFilePath))
                {
                    found.Add(current);
                    current = current._next;
                }

                // sort by priority
                return found
                    .OrderByDescending(s => s._priority)
                    .ToArray();
            }
        }

        /// <summary>
        /// Folder under which the config file is present
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Full path to the ConfigFile corresponding to this Settings object
        /// </summary>
        public string ConfigFilePath
        {
            get { return Path.GetFullPath(Path.Combine(Root, FileName)); }
        }

        /// <summary>
        /// Load default settings based on a directory.
        /// This includes machine wide settings.
        /// </summary>
        public static ISettings LoadDefaultSettings(string root)
        {
            return LoadDefaultSettings(
                root,
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting(),
                loadAppDataSettings: true,
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
            return LoadDefaultSettings(
                root,
                configFileName,
                machineWideSettings,
                loadAppDataSettings: true,
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

            return LoadDefaultSettings(
                root,
                configFileName,
                machineWideSettings: null,
                loadAppDataSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// For internal use only
        /// </summary>
        public static ISettings LoadDefaultSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings,
            bool loadAppDataSettings,
            bool useTestingGlobalPath)
        {
            {
                // Walk up the tree to find a config file; also look in .nuget subdirectories
                // If a configFile is passed, don't walk up the tree. Only use that single config file.
                var validSettingFiles = new List<Settings>();
                if (root != null && string.IsNullOrEmpty(configFileName))
                {
                    validSettingFiles.AddRange(
                        GetSettingsFileNames(root)
                            .Select(f => ReadSettings(root, f))
                            .Where(f => f != null));
                }

                if (loadAppDataSettings)
                {
                    LoadUserSpecificSettings(validSettingFiles, root, configFileName, machineWideSettings, useTestingGlobalPath);
                }

                if (machineWideSettings != null && string.IsNullOrEmpty(configFileName))
                {
                    validSettingFiles.AddRange(
                        machineWideSettings.Settings.Select(
                            s => new Settings(s.Root, s.FileName, s.IsMachineWideSettings)));
                }

                if (validSettingFiles == null
                    || !validSettingFiles.Any())
                {
                    // This means we've failed to load all config files and also failed to load or create the one in %AppData%
                    // Work Item 1531: If the config file is malformed and the constructor throws, NuGet fails to load in VS.
                    // Returning a null instance prevents us from silently failing and also from picking up the wrong config
                    return NullSettings.Instance;
                }

                SetClearTagForSettings(validSettingFiles);

                validSettingFiles[0]._priority = validSettingFiles.Count;

                // if multiple setting files were loaded, chain them in a linked list
                for (var i = 1; i < validSettingFiles.Count; ++i)
                {
                    validSettingFiles[i]._next = validSettingFiles[i - 1];
                    validSettingFiles[i]._priority = validSettingFiles[i - 1]._priority - 1;
                }

                // return the linked list head. Typicall, it's either the config file in %ProgramData%\NuGet\Config,
                // or the user specific config (%APPDATA%\NuGet\nuget.config) if there are no machine
                // wide config files. The head file is the one we want to read first, while the user specific config
                // is the one that we want to write to.
                // TODO: add UI to allow specifying which one to write to
                return validSettingFiles.Last();
            }
        }

        private static void LoadUserSpecificSettings(
            List<Settings> validSettingFiles,
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings,
            bool useTestingGlobalPath
            )
        {
            if (root == null)
            {
                // Path.Combine is performed with root so it should not be null
                // However, it is legal for it be empty in this method
                root = String.Empty;
            }
            // for the default location, allow case where file does not exist, in which case it'll end
            // up being created if needed
            Settings appDataSettings = null;
            if (configFileName == null)
            {
                var defaultSettingsFilePath = String.Empty;
                if (useTestingGlobalPath)
                {
                    defaultSettingsFilePath = Path.Combine(root, "TestingGlobalPath", DefaultSettingsFileName);
                }
                else
                {
                    var userSettingsDir = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);

                    // If there is no user settings directory, return no appdata settings
                    if (userSettingsDir == null)
                    {
                        return;
                    }
                    defaultSettingsFilePath = Path.Combine(userSettingsDir, DefaultSettingsFileName);
                }

                if (!File.Exists(defaultSettingsFilePath) && machineWideSettings != null)
                {

                    // Since defaultSettingsFilePath is a full path, so it doesn't matter what value is
                    // used as root for the PhysicalFileSystem.
                    appDataSettings = ReadSettings(
                    root,
                    defaultSettingsFilePath);

                    // Disable machinewide sources to improve perf
                    var disabledSources = new List<SettingValue>();
                    foreach (var setting in machineWideSettings.Settings)
                    {
                        var values = setting.GetSettingValues(ConfigurationConstants.PackageSources, isPath: true);
                        foreach (var value in values)
                        {
                            var packageSource = new PackageSource(value.Value);

                            // if the machine wide package source is http source, disable it by default
                            if (packageSource.IsHttp)
                            {
                                disabledSources.Add(new SettingValue(value.Key, "true", origin: setting, isMachineWide: true, priority: 0));
                            }
                        }
                    }
                    appDataSettings.UpdateSections(ConfigurationConstants.DisabledPackageSources, disabledSources);
                }
                else
                {
                    appDataSettings = ReadSettings(root, defaultSettingsFilePath);
                    bool IsEmptyConfig = !appDataSettings.GetSettingValues(ConfigurationConstants.PackageSources).Any();

                    if (IsEmptyConfig)
                    {
                        var trackFilePath = Path.Combine(Path.GetDirectoryName(defaultSettingsFilePath), NuGetConstants.AddV3TrackFile);

                        if (!File.Exists(trackFilePath))
                        {
                            File.Create(trackFilePath).Dispose();
                            var defaultPackageSource = new SettingValue(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, isMachineWide: false);
                            defaultPackageSource.AdditionalData.Add(ConfigurationConstants.ProtocolVersionAttribute, "3");
                            appDataSettings.UpdateSections(ConfigurationConstants.PackageSources, new List<SettingValue> { defaultPackageSource });
                        }
                    }
                }
            }
            else
            {
                if (!FileSystemUtility.DoesFileExistIn(root, configFileName))
                {
                    var message = String.Format(CultureInfo.CurrentCulture,
                        Resources.FileDoesNotExist,
                        Path.Combine(root, configFileName));
                    throw new InvalidOperationException(message);
                }

                appDataSettings = ReadSettings(root, configFileName);
            }

            if (appDataSettings != null)
            {
                validSettingFiles.Add(appDataSettings);
            }
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
        public static IEnumerable<Settings> LoadMachineWideSettings(
            string root,
            params string[] paths)
        {
            if (String.IsNullOrEmpty(root))
            {
                throw new ArgumentException("root cannot be null or empty");
            }

            var settingFiles = new List<Settings>();
            var combinedPath = Path.Combine(paths);

            while (true)
            {
                // load setting files in directory
                foreach (var file in FileSystemUtility.GetFilesRelativeToRoot(root, combinedPath, SupportedMachineWideConfigExtension, SearchOption.TopDirectoryOnly))
                {
                    var settings = ReadSettings(root, file, true);
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

            return settingFiles;
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(key));
            }

            XElement element = null;
            string ret = null;

            var curr = this;
            while (curr != null)
            {
                var newElement = curr.GetValueInternal(section, key, element);
                if (!ReferenceEquals(element, newElement))
                {
                    element = newElement;

                    // we need to evaluate using current Settings in case value needs path transformation
                    ret = curr.ElementToValue(element, isPath);
                }
                curr = curr._next;
            }

            return ret;
        }

        private string ApplyEnvironmentTransform(string configValue)
        {
            if (string.IsNullOrEmpty(configValue))
            {
                return configValue;
            }

            return Environment.ExpandEnvironmentVariables(configValue);
        }

        public IList<SettingValue> GetSettingValues(string section, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            var settingValues = new List<SettingValue>();
            var curr = this;
            while (curr != null)
            {
                curr.PopulateValues(section, settingValues, isPath);
                curr = curr._next;
            }

            return settingValues.AsReadOnly();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            if (String.IsNullOrEmpty(subSection))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(subSection));
            }

            var values = new List<SettingValue>();
            var curr = this;
            while (curr != null)
            {
                curr.PopulateNestedValues(section, subSection, values);
                curr = curr._next;
            }

            return values.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)).ToList().AsReadOnly();
        }

        public void SetValue(string section, string key, string value)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings)
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                _next.SetValue(section, key, value);
                return;
            }

            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }
            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            SetValueInternal(sectionElement, key, value, attributes: null);
            Save();
        }

        public void SetValues(string section, IReadOnlyList<SettingValue> values)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings)
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                _next.SetValues(section, values);
                return;
            }

            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            foreach (var value in values)
            {
                SetValueInternal(sectionElement, value.Key, value.Value, value.AdditionalData);
            }
            Save();
        }

        public void UpdateSections(string section, IReadOnlyList<SettingValue> values)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings ||
                ((section == ConfigurationConstants.PackageSources || section == ConfigurationConstants.DisabledPackageSources) && Cleared))
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                _next.UpdateSections(section, values);
                return;
            }

            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var valuesToWrite = _next == null ? values : values.Where(v => v.Priority < _next._priority);

            var sectionElement = GetSection(ConfigXDocument.Root, section);

            if (sectionElement == null && valuesToWrite.Any())
            {
                sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            }

            // When updating attempt to preserve the clear tag (and any sources that appear prior to it)
            // to avoid creating extra diffs in the source.
            RemoveElementAfterClearTag(sectionElement);

            foreach (var value in valuesToWrite)
            {
                var element = new XElement("add");
                SetElementValues(element, value.Key, value.OriginalValue, value.AdditionalData);
                XElementUtility.AddIndented(sectionElement, element);
            }

            Save();

            if (_next != null)
            {
                _next.UpdateSections(section, values.Where(v => v.Priority >= _next._priority).ToList());
            }
        }

        private static void RemoveElementAfterClearTag(XElement sectionElement)
        {
            if (sectionElement == null)
            {
                return;
            }

            var nodesToRemove = new List<XNode>();
            foreach (var node in sectionElement.Nodes())
            {
                if (node.NodeType != XmlNodeType.Element)
                {
                    nodesToRemove.Add(node);
                    continue;
                }

                var element = (XElement)node;

                if (element.Name.LocalName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    nodesToRemove.Clear();
                }
                else
                {
                    nodesToRemove.Add(element);
                }
            }

            // Special case for the scenario where the clear element is the last element in the
            // section (followed by whitespace and comments). In this case, we can avoid removing any
            // node and preserving the original formatting.
            if (nodesToRemove.Any(node => node.NodeType == XmlNodeType.Element))
            {
                foreach (var element in nodesToRemove)
                {
                    element.Remove();
                }
            }
        }

        private static void SetElementValues(XElement element, string key, string value, IDictionary<string, string> attributes)
        {
            foreach (var existingAttribute in element.Attributes())
            {
                if (!string.Equals(existingAttribute.Name.LocalName, ConfigurationConstants.KeyAttribute, StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(existingAttribute.Name.LocalName, ConfigurationConstants.ValueAttribute, StringComparison.OrdinalIgnoreCase)
                    &&
                    !attributes.ContainsKey(existingAttribute.Name.LocalName))
                {
                    // Remove previously existing attributes that are no longer present.
                    existingAttribute.Remove();
                }
            }

            element.SetAttributeValue(ConfigurationConstants.KeyAttribute, key);
            element.SetAttributeValue(ConfigurationConstants.ValueAttribute, value);

            if (attributes != null)
            {
                foreach (var attribute in attributes)
                {
                    element.SetAttributeValue(attribute.Key, attribute.Value);
                }
            }
        }

        public void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings)
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                _next.SetNestedValues(section, key, values);
                return;
            }

            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            var element = GetOrCreateSection(sectionElement, key);

            foreach (var kvp in values)
            {
                SetValueInternal(element, kvp.Key, kvp.Value, attributes: null);
            }
            Save();
        }

        public bool DeleteValue(string section, string key)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings)
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                return _next.DeleteValue(section, key);
            }

            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(key));
            }

            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement == null)
            {
                return false;
            }

            var elementToDelete = FindElementByKey(sectionElement, key, null);
            if (elementToDelete == null)
            {
                return false;
            }
            XElementUtility.RemoveIndented(elementToDelete);
            Save();
            return true;
        }

        public bool DeleteSection(string section)
        {
            // machine wide settings cannot be changed.
            if (IsMachineWideSettings)
            {
                if (_next == null)
                {
                    throw new InvalidOperationException(Resources.Error_NoWritableConfig);
                }

                return _next.DeleteSection(section);
            }

            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement == null)
            {
                return false;
            }

            XElementUtility.RemoveIndented(sectionElement);
            Save();
            return true;
        }

        private XElement GetValueInternal(string section, string key, XElement curr)
        {
            // Get the section and return curr if it doesn't exist
            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement == null)
            {
                return curr;
            }

            // Get the add element that matches the key and return curr if it doesn't exist
            return FindElementByKey(sectionElement, key, curr);
        }

        private static XElement GetSection(XElement parentElement, string section)
        {
            section = XmlConvert.EncodeLocalName(section);
            return parentElement.Element(section);
        }

        private static XElement GetOrCreateSection(XElement parentElement, string sectionName)
        {
            sectionName = XmlConvert.EncodeLocalName(sectionName);
            var section = parentElement.Element(sectionName);
            if (section == null)
            {
                section = new XElement(sectionName);
                XElementUtility.AddIndented(parentElement, section);
            }
            return section;
        }

        private static XElement FindElementByKey(XElement sectionElement, string key, XElement curr)
        {
            var result = curr;
            foreach (var element in sectionElement.Elements())
            {
                var elementName = element.Name.LocalName;
                if (elementName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    result = null;
                }
                else if (elementName.Equals("add", StringComparison.OrdinalIgnoreCase)
                         &&
                         XElementUtility.GetOptionalAttributeValue(element, ConfigurationConstants.KeyAttribute).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    result = element;
                }
            }
            return result;
        }

        private string ElementToValue(XElement element, bool isPath)
        {
            if (element == null)
            {
                return null;
            }

            // Return the optional value which if not there will be null;
            var value = XElementUtility.GetOptionalAttributeValue(element, ConfigurationConstants.ValueAttribute);
            value = ApplyEnvironmentTransform(value);
            if (!isPath
                || String.IsNullOrEmpty(value))
            {
                return value;
            }
            return Path.Combine(Root, ResolvePath(Path.GetDirectoryName(ConfigFilePath), value));
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

        private void PopulateValues(string section, List<SettingValue> current, bool isPath)
        {
            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement != null)
            {
                ReadSection(sectionElement, current, isPath);
            }
        }

        private void PopulateNestedValues(string section, string subSection, List<SettingValue> current)
        {
            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement == null)
            {
                return;
            }
            var subSectionElement = GetSection(sectionElement, subSection);
            if (subSectionElement == null)
            {
                return;
            }
            ReadSection(subSectionElement, current, isPath: false);
        }

        private void ReadSection(XContainer sectionElement, ICollection<SettingValue> values, bool isPath)
        {
            var elements = sectionElement.Elements();

            foreach (var element in elements)
            {
                var elementName = element.Name.LocalName;
                if (elementName.Equals("add", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(ReadSettingsValue(element, isPath));
                }
                else if (elementName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    values.Clear();
                }
            }
        }

        private SettingValue ReadSettingsValue(XElement element, bool isPath)
        {
            var keyAttribute = element.Attribute(ConfigurationConstants.KeyAttribute);
            var valueAttribute = element.Attribute(ConfigurationConstants.ValueAttribute);

            if (keyAttribute == null
                || String.IsNullOrEmpty(keyAttribute.Value)
                || valueAttribute == null)
            {
                throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, ConfigFilePath));
            }

            var value = ApplyEnvironmentTransform(valueAttribute.Value);
            var originalValue = valueAttribute.Value;
            Uri uri;

            if (isPath && Uri.TryCreate(value, UriKind.Relative, out uri))
            {
                var configDirectory = Path.GetDirectoryName(ConfigFilePath);
                value = Path.Combine(Root, Path.Combine(configDirectory, value));
            }

            var settingValue = new SettingValue(keyAttribute.Value,
                                                value,
                                                origin: this,
                                                isMachineWide: IsMachineWideSettings,
                                                originalValue: originalValue,
                                                priority: _priority);
            foreach (var attribute in element.Attributes())
            {
                // Add all attributes other than ConfigurationContants.KeyAttribute and ConfigurationContants.ValueAttribute to AdditionalValues
                if (!string.Equals(attribute.Name.LocalName, ConfigurationConstants.KeyAttribute, StringComparison.Ordinal)
                    &&
                    !string.Equals(attribute.Name.LocalName, ConfigurationConstants.ValueAttribute, StringComparison.Ordinal))
                {
                    settingValue.AdditionalData[attribute.Name.LocalName] = attribute.Value;
                }
            }

            return settingValue;
        }

        private void SetValueInternal(XElement sectionElement, string key, string value, IDictionary<string, string> attributes)
        {
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, ConfigurationConstants.KeyAttribute);
            }
            if (value == null)
            {
                throw new ArgumentNullException(ConfigurationConstants.ValueAttribute);
            }

            var element = FindElementByKey(sectionElement, key, null);

            if (element != null)
            {
                SetElementValues(element, key, value, attributes);
                Save();
            }
            else
            {
                element = new XElement("add");
                SetElementValues(element, key, value, attributes);
                XElementUtility.AddIndented(sectionElement, element);
            }
        }

        private static Settings ReadSettings(string root, string settingsPath, bool isMachineWideSettings = false)
        {
            try
            {
                var tuple = GetFileNameAndItsRoot(root, settingsPath);
                var fileName = tuple.Item1;
                root = tuple.Item2;
                return new Settings(root, fileName, isMachineWideSettings);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        public static Tuple<string, string> GetFileNameAndItsRoot(string root, string settingsPath)
        {
            string fileName = null;
            if (Path.IsPathRooted(settingsPath))
            {
                root = Path.GetDirectoryName(settingsPath);
                fileName = Path.GetFileName(settingsPath);
            }
            else if (!FileSystemUtility.IsPathAFile(settingsPath))
            {
                var fullPath = Path.Combine(root ?? String.Empty, settingsPath);
                root = Path.GetDirectoryName(fullPath);
                fileName = Path.GetFileName(fullPath);
            }
            else
            {
                fileName = settingsPath;
            }

            return new Tuple<string, string>(fileName, root);
        }

        /// <remarks>
        /// Order is most significant (e.g. applied last) to least significant (applied first)
        /// ex:
        /// c:\someLocation\nuget.config
        /// c:\nuget.config
        /// </remarks>
        private static IEnumerable<string> GetSettingsFileNames(string root)
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

        private void Save()
        {
            ExecuteSynchronized(() => FileSystemUtility.AddFile(ConfigFilePath, ConfigXDocument.Save));
        }

#if IS_CORECLR
        private static Mutex _globalMutex = new Mutex(initiallyOwned: false);

        /// <summary>
        /// Wrap all IO operations on setting files with this function to avoid file-in-use errors
        /// </summary>
        private void ExecuteSynchronized(Action ioOperation)
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                ExecuteSynchronizedCore(ioOperation);
                return;
            }
            else
            {
                // Cross-plat CoreCLR doesn't support named lock, so we fall back to
                // process-local synchronization in this case
                var owner = false;
                try
                {
                    owner = _globalMutex.WaitOne(TimeSpan.FromMinutes(1));
                    ioOperation();
                }
                catch (InvalidOperationException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture,Resources.ShowError_ConfigInvalidOperation, ConfigFilePath, e.Message), e);
                }

                catch (UnauthorizedAccessException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture,Resources.ShowError_ConfigUnauthorizedAccess, ConfigFilePath, e.Message), e);
                }

                catch (XmlException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture,Resources.ShowError_ConfigInvalidXml, ConfigFilePath, e.Message), e);
                }

                catch (Exception e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture,Resources.Unknown_Config_Exception, ConfigFilePath, e.Message), e);
                }
                finally
                {
                    if (owner)
                    {
                        _globalMutex.ReleaseMutex();
                    }
                }
            }
        }
#else
        /// <summary>
        /// Wrap all IO operations on setting files with this function to avoid file-in-use errors
        /// </summary>
        private void ExecuteSynchronized(Action ioOperation)
        {
            ExecuteSynchronizedCore(ioOperation);
        }
#endif
        private void ExecuteSynchronizedCore(Action ioOperation)
        {
            var fileName = ConfigFilePath;

            // Global: ensure mutex is honored across TS sessions 
            using (var mutex = new Mutex(false, $"Global\\{EncryptionUtility.GenerateUniqueToken(fileName)}"))
            {
                var owner = false;
                try
                {
                    // operations on NuGet.config should be very short lived
                    owner = mutex.WaitOne(TimeSpan.FromMinutes(1));
                    // decision here is to proceed even if we were not able to get mutex ownership
                    // and let the potential IO errors bubble up. Reasoning is that failure to get
                    // ownership probably means faulty hardware and in this case it's better to report
                    // back than hang
                    ioOperation();
                }
                catch (InvalidOperationException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigInvalidOperation, fileName, e.Message), e);
                }

                catch (UnauthorizedAccessException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigUnauthorizedAccess, fileName, e.Message), e);
                }

                catch (XmlException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigInvalidXml, fileName, e.Message), e);
                }

                catch (Exception e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.Unknown_Config_Exception, fileName, e.Message), e);
                }
                finally
                {
                    if (owner)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static XDocument CreateDefaultConfig()
        {
            return new XDocument(new XElement("configuration",
                                 new XElement(ConfigurationConstants.PackageSources,
                                 new XElement("add",
                                 new XAttribute(ConfigurationConstants.KeyAttribute, NuGetConstants.FeedName),
                                 new XAttribute(ConfigurationConstants.ValueAttribute, NuGetConstants.V3FeedUrl),
                                 new XAttribute(ConfigurationConstants.ProtocolVersionAttribute, "3")))));
        }

        private static void SetClearTagForSettings(List<Settings> settings)
        {
            var result = new List<Settings>();

            bool foundClear = false;

            foreach (var setting in settings)
            {
                if (!foundClear)
                {
                    foundClear = FoundClearTag(setting.ConfigXDocument);
                }
                else
                {
                    setting.Cleared = true;
                }
            }
        }

        private static bool FoundClearTag(XDocument config)
        {
            var sectionElement = GetSection(config.Root, ConfigurationConstants.PackageSources);
            if (sectionElement != null)
            {
                foreach (var element in sectionElement.Elements())
                {
                    if (element.Name.LocalName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // this method will check NuGet.Config file, if the root is not configuration, it will throw.
        private void CheckConfigRoot()
        {
            if (ConfigXDocument.Root.Name != "configuration")
            {
                throw new NuGetConfigurationException(
                         string.Format(Resources.ShowError_ConfigRootInvalid, ConfigFilePath));
            }
        }
    }
}
