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
        private const string TestProjectName = "TestProjectName";
        public HashSet<string> References { get; private set; }
        public HashSet<string> FrameworkReferences { get; private set; }
        public HashSet<string> ContentFiles { get; private set; }

        public TestMSBuildNuGetProjectSystem(NuGetFramework targetFramework)
        {
            TargetFramework = targetFramework;
            References = new HashSet<string>();
            FrameworkReferences = new HashSet<string>();
            ContentFiles = new HashSet<string>();
        }

        public void AddFile(string path, Stream stream)
        {
            ContentFiles.Add(path);
        }

        public void AddFrameworkReference(string name)
        {
            FrameworkReferences.Add(name);
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
            ContentFiles.Remove(path);
        }

        public string ProjectFullPath
        {
            get { return Environment.CurrentDirectory; }
        }

        public string ProjectName
        {
            get { return TestProjectName; }
        }

        public bool ReferenceExists(string name)
        {
            return References.Where(r => name.Equals(r, StringComparison.OrdinalIgnoreCase)).Any();
        }

        public void RemoveImport(string targetFullPath)
        {
            throw new NotImplementedException();
        }

        public void RemoveReference(string name)
        {
            References.Remove(name);
        }

        public NuGetFramework TargetFramework
        {
            get;
            private set;
        }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            // No-op
        }


        public bool FileExistsInProject(string path)
        {
            throw new NotImplementedException();
        }

        public dynamic GetPropertyValue(string propertyName)
        {
            throw new NotImplementedException();
        }

        public bool IsSupportedFile(string path)
        {
            throw new NotImplementedException();
        }

        public INuGetProjectContext NuGetProjectContext
        {
            get { throw new NotImplementedException(); }
        }

        public void RemoveFile(string path)
        {
            throw new NotImplementedException();
        }

        public string ResolvePath(string path)
        {
            throw new NotImplementedException();
        }
    }
}
