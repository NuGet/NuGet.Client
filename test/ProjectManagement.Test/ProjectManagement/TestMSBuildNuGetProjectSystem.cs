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
        public Dictionary<string, string> References { get; private set; }
        public HashSet<string> FrameworkReferences { get; private set; }
        public HashSet<string> Files { get; private set; }
        public INuGetProjectContext NuGetProjectContext { get; private set; }

        public TestMSBuildNuGetProjectSystem(NuGetFramework targetFramework, INuGetProjectContext nuGetProjectContext, string projectFullPath = null)
        {
            TargetFramework = targetFramework;
            References = new Dictionary<string, string>();
            FrameworkReferences = new HashSet<string>();
            Files = new HashSet<string>();
            NuGetProjectContext = nuGetProjectContext;
            ProjectFullPath = String.IsNullOrEmpty(projectFullPath) ? Environment.CurrentDirectory : projectFullPath;
        }

        public void AddFile(string path, Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                Files.Add(path);
                var fullPath = Path.Combine(ProjectFullPath, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, streamReader.ReadToEnd());
            }
        }

        public void AddFrameworkReference(string name)
        {
            if(FrameworkReferences.Contains(name))
            {
                throw new InvalidOperationException("Cannot add existing reference. That would be a COMException in VS");
            }
            FrameworkReferences.Add(name);
        }

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            throw new NotImplementedException();
        }

        public void AddReference(string referencePath)
        {
            var referenceAssemblyName = Path.GetFileName(referencePath);
            if (References.ContainsKey(referenceAssemblyName))
            {
                throw new InvalidOperationException("Cannot add existing reference. That would be a COMException in VS");
            }
            References.Add(referenceAssemblyName, referencePath);
        }

        public void RemoveFile(string path)
        {
            Files.Remove(path);
            string fullPath = Path.Combine(ProjectFullPath, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public string ProjectFullPath
        {
            get;
            private set;
        }

        public string ProjectName
        {
            get { return TestProjectName; }
        }

        public bool ReferenceExists(string name)
        {
            return References.ContainsKey(name);
        }

        public void RemoveImport(string targetFullPath)
        {
            throw new NotImplementedException();
        }

        public void RemoveReference(string name)
        {            
            if(References.ContainsKey(name))
            {
                References.Remove(name);
            }
        }

        public NuGetFramework TargetFramework
        {
            get;
            private set;
        }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            NuGetProjectContext = nuGetProjectContext;
        }

        public bool FileExistsInProject(string path)
        {
            return Files.Where(c => path.Equals(c, StringComparison.OrdinalIgnoreCase)).Any();
        }

        public dynamic GetPropertyValue(string propertyName)
        {
            throw new NotImplementedException();
        }

        public string ResolvePath(string path)
        {
            return path;
        }

        public bool IsSupportedFile(string path)
        {
            return true;
        }

        public void AddExistingFile(string path)
        {
            Files.Add(path);
        }


        public void AddBindingRedirects()
        {
            // No-op
        }
    }
}
