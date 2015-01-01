using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public class Settings : ISettings
    {
        public const string DefaultSettingsFileName = "NuGet.Config";
        private XDocument Config { get; set; }
        private string Root { get; set; }
        private string FileName { get; set; }

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
            Root = root;
            FileName = fileName;
            XDocument config = null;
//            ExecuteSynchronized(() => config = XmlUtility.GetOrCreateDocument("configuration", _fileSystem, _fileName));
            Config = config;
            _isMachineWideSettings = isMachineWideSettings;
        }

        public string GetValue(string section, string key)
        {
            throw new NotImplementedException();
        }

        public string GetValue(string section, string key, bool isPath)
        {
            throw new NotImplementedException();
        }

        public IList<SettingValue> GetSettingValues(string section, bool isPath)
        {
            throw new NotImplementedException();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string key)
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

        public void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values)
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
    }
}
