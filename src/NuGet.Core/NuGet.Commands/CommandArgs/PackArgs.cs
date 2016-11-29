using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Configuration;
using NuGet.Common;

namespace NuGet.Commands
{
    public class PackArgs
    {
        private string _currentDirectory;
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Arguments { get; set; }
        public string BasePath { get; set; }
        public bool Build { get; set; }
        public IEnumerable<string> Exclude { get; set; }
        public bool ExcludeEmptyDirectories { get; set; }
        public ILogger Logger { get; set; }
        public LogLevel LogLevel { get; set; }
        public bool IncludeReferencedProjects { get; set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public Version MinClientVersion { get; set; }
        public Lazy<string> MsBuildDirectory { get; set; }
        public bool NoDefaultExcludes { get; set; }
        public bool NoPackageAnalysis { get; set; }
        public string OutputDirectory { get; set; }
        public string Path { get; set; }
        public bool Serviceable { get; set; }
        public string Suffix { get; set; }
        public bool Symbols { get; set; }
        public bool Tool { get; set; }
        public string Version { get; set; }
        public MSBuildPackTargetArgs PackTargetArgs { get; set; }
        public Dictionary<string, string> Properties
        {
            get
            {
                return _properties;
            }
        }

        public string CurrentDirectory
        {
            get
            {
                return _currentDirectory ?? Directory.GetCurrentDirectory();
            }
            set
            {
                _currentDirectory = value;
            }
        }

        public string GetPropertyValue(string propertyName)
        {
            string value;
            if (Properties.TryGetValue(propertyName, out value))
            {
                return value;
            }
            return null;
        }
    }
}
