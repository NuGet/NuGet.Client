using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Utility
{
    public class TestSolutionManager : ISolutionManager
    {
        private List<NuGetProject> NuGetProjects { get; set; }
        public string SolutionDirectory { get; private set; }
        private const string PackagesFolder = "packages";

        public TestSolutionManager()
        {
            SolutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            NuGetProjects = new List<NuGetProject>();
        }

        public NuGetProject AddNewMSBuildProject(NuGetFramework projectTargetFramework = null, string packagesConfigName = null)
        {
            var packagesFolder = Path.Combine(SolutionDirectory, PackagesFolder);
            var projectName = Guid.NewGuid().ToString();
            var projectFullPath = Path.Combine(SolutionDirectory, projectName);
            Directory.CreateDirectory(projectFullPath);
            var packagesConfigPath = Path.Combine(projectFullPath, packagesConfigName ?? "packages.config");

            projectTargetFramework = projectTargetFramework ?? NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext(),
                projectFullPath, projectName);
            NuGetProject nuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolder, packagesConfigPath);
            NuGetProjects.Add(nuGetProject);
            return nuGetProject;
        }

        public NuGetProject DefaultNuGetProject
        {
            get
            {
                return NuGetProjects.FirstOrDefault();
            }
        }

        public string DefaultNuGetProjectName
        {
            get
            {
                return DefaultNuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            return NuGetProjects.
                Where(p => string.Equals(nuGetProjectSafeName, p.GetMetadata<string>(NuGetProjectMetadataKeys.Name), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            return nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
        }

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            return NuGetProjects;
        }

        public bool IsSolutionOpen
        {
            get
            {
                return NuGetProjects.Count > 0;
            }
        }

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public event EventHandler SolutionOpened;
    }
}
