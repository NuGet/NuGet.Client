using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandaloneUI
{
    [Export(typeof(ISettings))]
    public class DefaultSettings : ISettings
    {
        private NuGet.Configuration.ISettings Instance { get; set; }

        public DefaultSettings()
        {
            Instance = Settings.LoadDefaultSettings(null, null, null);
        }

        public bool DeleteSection(string section)
        {
            throw new NotImplementedException();
        }

        public bool DeleteValue(string section, string key)
        {
            throw new NotImplementedException();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            return Instance.GetNestedValues(section, subSection);
        }

        public IList<SettingValue> GetSettingValues(string section)
        {
            return Instance.GetSettingValues(section);
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return Instance.GetValue(section, key, isPath);
        }

        public string Root
        {
            get { return Instance.Root; }
        }

        public void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values)
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
    }

    public class MySolutionManager : NuGet.PackageManagement.ISolutionManager
    {
        public NuGet.ProjectManagement.NuGetProject DefaultNuGetProject
        {
            get { throw new NotImplementedException(); }
        }

        public string DefaultNuGetProjectName
        {
            get { throw new NotImplementedException(); }
        }

        public NuGet.ProjectManagement.NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            throw new NotImplementedException();
        }

        public string GetNuGetProjectSafeName(NuGet.ProjectManagement.NuGetProject nuGetProject)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<NuGet.ProjectManagement.NuGetProject> GetNuGetProjects()
        {
            throw new NotImplementedException();
        }

        public bool IsSolutionOpen
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler<NuGet.PackageManagement.NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public string SolutionDirectory
        {
            get { return @"c:\temp"; }
        }

        public event EventHandler SolutionOpened;


        string NuGet.PackageManagement.ISolutionManager.DefaultNuGetProjectName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public NuGet.ProjectManagement.INuGetProjectContext NuGetProjectContext
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }
}
