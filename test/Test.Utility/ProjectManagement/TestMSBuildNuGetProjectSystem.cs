using NuGet.Frameworks;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Utility
{
    public class TestMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        internal TestMSBuildNuGetProjectSystem(NuGetFramework targetFramework)
        {
            TargetFramework = targetFramework;
            References = new List<string>();
        }

        public void AddFile(string path, Stream stream)
        {
            throw new NotImplementedException();
        }

        public void AddFrameworkReference(string name)
        {
            throw new NotImplementedException();
        }

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            throw new NotImplementedException();
        }

        public void AddReference(string referencePath)
        {
            References.Add(referencePath);
        }

        public void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public string ProjectFullPath
        {
            get { throw new NotImplementedException(); }
        }

        public string ProjectName
        {
            get { throw new NotImplementedException(); }
        }

        public bool ReferenceExists(string name)
        {
            throw new NotImplementedException();
        }

        public void RemoveImport(string targetFullPath)
        {
            throw new NotImplementedException();
        }

        public void RemoveReference(string name)
        {
            throw new NotImplementedException();
        }

        public NuGetFramework TargetFramework
        {
            get;
            private set;
        }

        public List<string> References { get; private set; }
    }
}
