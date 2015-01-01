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

        // next config file to read if any
        private Settings _next;

        private readonly bool _isMachineWideSettings;

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
                throw new ArgumentException(NuGet_Configuration_Resources.Argument_Cannot_Be_Null_Or_Empty, "fileName");
            }

            if(!String.Equals(fileName, Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(NuGet_Configuration_Resources.Settings_FileName_Cannot_Be_A_Path);
            }

            Root = root;
            ConfigFileName = fileName;
            XDocument config = null;
            ExecuteSynchronized(() => config = XmlUtility.GetOrCreateDocument("configuration", ConfigFilePath));
            ConfigXDocument = config;
            _isMachineWideSettings = isMachineWideSettings;
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

        public string GetValue(string section, string key, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(NuGet_Configuration_Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(NuGet_Configuration_Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
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
            throw new NotImplementedException();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            throw new NotImplementedException();
        }

        public void SetValue(string section, string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            throw new NotImplementedException();
        }

        public void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values)
        {
            throw new NotImplementedException();
        }

        public bool DeleteValue(string section, string key)
        {
            throw new NotImplementedException();
        }

        public bool DeleteSection(string section)
        {
            throw new NotImplementedException();
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
                         XmlUtility.GetOptionalAttributeValue(element, "key").Equals(key, StringComparison.OrdinalIgnoreCase))
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
            string value = XmlUtility.GetOptionalAttributeValue(element, "value");
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
