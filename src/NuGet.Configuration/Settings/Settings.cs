using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

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
        public const string DefaultSettingsFileName = "NuGet.config";

        private XDocument ConfigXDocument { get; set; }
        private string ConfigFileName { get; set; }
        private bool IsMachineWideSettings { get; set; }
        // next config file to read if any
        private Settings _next;
        // The priority of this setting file
        private int _priority;

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
                throw new ArgumentException("root cannot be null or empty");
            }

            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "fileName");
            }

            if(!FileSystemUtility.IsPathAFile(fileName))
            {
                throw new ArgumentException(Resources.Settings_FileName_Cannot_Be_A_Path);
            }

            Root = root;
            ConfigFileName = fileName;
            XDocument config = null;
            ExecuteSynchronized(() => config = XmlUtility.GetOrCreateDocument("configuration", ConfigFilePath));
            ConfigXDocument = config;
            IsMachineWideSettings = isMachineWideSettings;
        }

        /// <summary>
        /// Folder under which the config file is present
        /// </summary>
        public string Root
        {
            get;
            private set;
        }

        /// <summary>
        /// Full path to the ConfigFile corresponding to this Settings object
        /// </summary>
        public string ConfigFilePath
        {
            get
            {
                return Path.GetFullPath(Path.Combine(Root, ConfigFileName));
            }
        }

        /// <summary>
        /// Loads user settings from the NuGet configuration files. The method walks the directory
        /// tree in <paramref name="fileSystem"/> up to its root, and reads each NuGet.config file
        /// it finds in the directories. It then reads the user specific settings,
        /// which is file <paramref name="configFileName"/>
        /// in <paramref name="fileSystem"/> if <paramref name="configFileName"/> is not null,
        /// If <paramref name="configFileName"/> is null, the user specific settings file is
        /// %AppData%\NuGet\NuGet.config.
        /// After that, the machine wide settings files are added.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="fileSystem"/> is c:\dir1\dir2, <paramref name="configFileName"/>
        /// is "userConfig.file", the files loaded are (in the order that they are loaded):
        ///     c:\dir1\dir2\nuget.config
        ///     c:\dir1\nuget.config
        ///     c:\nuget.config
        ///     c:\dir1\dir2\userConfig.file
        ///     machine wide settings (e.g. c:\programdata\NuGet\Config\*.config)
        /// </remarks>
        /// <param name="fileSystem">The file system to walk to find configuration files.
        /// Can be null.</param>
        /// <param name="configFileName">The user specified configuration file.</param>
        /// <param name="machineWideSettings">The machine wide settings. If it's not null, the
        /// settings files in the machine wide settings are added after the user sepcific
        /// config file.</param>
        /// <returns>The settings object loaded.</returns>
        public static ISettings LoadDefaultSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings)
        {
            // Walk up the tree to find a config file; also look in .nuget subdirectories
            var validSettingFiles = new List<Settings>();
            if (root != null)
            {
                validSettingFiles.AddRange(
                    GetSettingsFileNames(root)
                        .Select(f => ReadSettings(root, f))
                        .Where(f => f != null));
            }

            LoadUserSpecificSettings(validSettingFiles, root, configFileName);

            if (machineWideSettings != null)
            {
                validSettingFiles.AddRange(
                    machineWideSettings.Settings.Select(
                        s => new Settings(s.Root, s.ConfigFileName, s.IsMachineWideSettings)));
            }

            if (validSettingFiles == null || !validSettingFiles.Any())
            {
                // This means we've failed to load all config files and also failed to load or create the one in %AppData%
                // Work Item 1531: If the config file is malformed and the constructor throws, NuGet fails to load in VS.
                // Returning a null instance prevents us from silently failing and also from picking up the wrong config
                return NullSettings.Instance;
            }

            validSettingFiles[0]._priority = validSettingFiles.Count;

            // if multiple setting files were loaded, chain them in a linked list
            for (int i = 1; i < validSettingFiles.Count; ++i)
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

        private static void LoadUserSpecificSettings(
            List<Settings> validSettingFiles,
            string root,
            string configFileName)
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
                // load %AppData%\NuGet\NuGet.config
#if !DNXCORE50
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
                string appDataPath = Environment.GetEnvironmentVariable("AppData");
#endif
                if (!String.IsNullOrEmpty(appDataPath))
                {
                    var defaultSettingsFilePath = Path.Combine(
                        appDataPath, "NuGet", DefaultSettingsFileName);

                    // Since defaultSettingsFilePath is a full path, so it doesn't matter what value is
                    // used as root for the PhysicalFileSystem.
                    appDataSettings = ReadSettings(
                        root,
                        defaultSettingsFilePath);
                }
            }
            else
            {
                if (!FileSystemUtility.DoesFileExistIn(root, configFileName))
                {
                    string message = String.Format(CultureInfo.CurrentCulture,
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
        /// For example, if <paramref name="paths"/> is {"IDE", "Version", "SKU" }, then
        /// the files loaded are (in the order that they are loaded):
        ///     %programdata%\NuGet\Config\IDE\Version\SKU\*.config
        ///     %programdata%\NuGet\Config\IDE\Version\*.config
        ///     %programdata%\NuGet\Config\IDE\*.config
        ///     %programdata%\NuGet\Config\*.config
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

            List<Settings> settingFiles = new List<Settings>();
            string basePath = @"NuGet\Config";
            string combinedPath = Path.Combine(paths);

            while (true)
            {
                string directory = Path.Combine(basePath, combinedPath);

                // load setting files in directory
                foreach (var file in FileSystemUtility.GetFilesRelativeToRoot(root, directory, "*.config", SearchOption.TopDirectoryOnly))
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

                int index = combinedPath.LastIndexOf(Path.DirectorySeparatorChar);
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            XElement element = null;
            string ret = null;

            var curr = this;
            while (curr != null)
            {
                XElement newElement = curr.GetValueInternal(section, key, element);
                if (!object.ReferenceEquals(element, newElement))
                {
                    element = newElement;

                    // we need to evaluate using current Settings in case value needs path transformation
                    ret = curr.ElementToValue(element, isPath);
                }
                curr = curr._next;
            }

            return ret;
        }

        public IList<SettingValue> GetSettingValues(string section)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            var settingValues = new List<SettingValue>();
            var curr = this;
            while (curr != null)
            {
                curr.PopulateValues(section, settingValues);
                curr = curr._next;
            }

            return settingValues.AsReadOnly();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(subSection))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "subSection");
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            SetValueInternal(sectionElement, key, value);
            Save();
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            foreach (var kvp in values)
            {
                SetValueInternal(sectionElement, kvp.Key, kvp.Value);
            }
            Save();
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            var sectionElement = GetOrCreateSection(ConfigXDocument.Root, section);
            var element = GetOrCreateSection(sectionElement, key);

            foreach (var kvp in values)
            {
                SetValueInternal(element, kvp.Key, kvp.Value);
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
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
            XElement result = curr;
            foreach (var element in sectionElement.Elements())
            {
                string elementName = element.Name.LocalName;
                if (elementName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    result = null;
                }
                else if (elementName.Equals("add", StringComparison.OrdinalIgnoreCase) &&
                         XElementUtility.GetOptionalAttributeValue(element, "key").Equals(key, StringComparison.OrdinalIgnoreCase))
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
            string value = XElementUtility.GetOptionalAttributeValue(element, "value");
            if (!isPath || String.IsNullOrEmpty(value))
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
            if (root != null && root.Length == 1 && (root[0] == Path.DirectorySeparatorChar || value[0] == Path.AltDirectorySeparatorChar))
            {
                return Path.Combine(Path.GetPathRoot(configDirectory), value.Substring(1));
            }
            return Path.Combine(configDirectory, value);
        }

        private void PopulateValues(string section, List<SettingValue> current)
        {
            var sectionElement = GetSection(ConfigXDocument.Root, section);
            if (sectionElement != null)
            {
                ReadSection(sectionElement, current);
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
            ReadSection(subSectionElement, current);
        }

        private void ReadSection(XContainer sectionElement, ICollection<SettingValue> values)
        {
            var elements = sectionElement.Elements();

            foreach (var element in elements)
            {
                string elementName = element.Name.LocalName;
                if (elementName.Equals("add", StringComparison.OrdinalIgnoreCase))
                {
                    var v = ReadValue(element);
                    values.Add(new SettingValue(v.Key, v.Value, IsMachineWideSettings, _priority));
                }
                else if (elementName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    values.Clear();
                }
            }
        }

        private KeyValuePair<string, string> ReadValue(XElement element)
        {
            var keyAttribute = element.Attribute("key");
            var valueAttribute = element.Attribute("value");

            if (keyAttribute == null || String.IsNullOrEmpty(keyAttribute.Value) || valueAttribute == null)
            {
                throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, ConfigFilePath));
            }

            var value = valueAttribute.Value;
            //Uri uri;
            //if (isPath && Uri.TryCreate(value, UriKind.Relative, out uri))
            //{
            //    string configDirectory = Path.GetDirectoryName(ConfigFilePath);
            //    value = _fileSystem.GetFullPath(Path.Combine(configDirectory, value));
            //}

            return new KeyValuePair<string, string>(keyAttribute.Value, value);
        }

        private void SetValueInternal(XElement sectionElement, string key, string value)
        {
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var element = FindElementByKey(sectionElement, key, null);
            if (element != null)
            {
                element.SetAttributeValue("value", value);
                Save();
            }
            else
            {
                XElementUtility.AddIndented(sectionElement, new XElement("add",
                                                            new XAttribute("key", key),
                                                            new XAttribute("value", value)));
            }
        }

        private static Settings ReadSettings(string root, string settingsPath, bool isMachineWideSettings = false)
        {
            try
            {
                var tuple = GetFileNameAndItsRoot(root, settingsPath);
                string fileName = tuple.Item1;
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
        /// c:\foo\nuget.config
        /// c:\nuget.config
        /// </remarks>
        private static IEnumerable<string> GetSettingsFileNames(string root)
        {
            // for dirs obtained by walking up the tree, only consider setting files that already exist.
            // otherwise we'd end up creating them.
            foreach (var dir in GetSettingsFilePaths(root))
            {
                string fileName = Path.Combine(dir, DefaultSettingsFileName);
                if (FileSystemUtility.DoesFileExistIn(root, fileName))
                {
                    yield return fileName;
                }
            }
        }

        private static IEnumerable<string> GetSettingsFilePaths(string root)
        {
            while (root != null)
            {
                yield return root;
                root = Path.GetDirectoryName(root);
            }
        }

        private void Save()
        {
            ExecuteSynchronized(() => FileSystemUtility.AddFile(ConfigFilePath, ConfigXDocument.Save));
        }

        /// <summary>
        /// Wrap all IO operations on setting files with this function to avoid file-in-use errors
        /// </summary>
        private void ExecuteSynchronized(Action ioOperation)
        {
            var configFilePath = ConfigFilePath;

            // Global: ensure mutex is honored across TS sessions
            using (var mutex = new Mutex(false, "Global\\" + EncryptionUtility.GenerateUniqueToken(configFilePath)))
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
                finally
                {
                    if (owner)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }
    }
}
