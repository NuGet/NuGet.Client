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
        private List<NuGetProject> msbuildNuGetProjects;

        public TestSolutionManager()
        {
            NuGetProject projectA = CreateNewMSBuildProject();
            NuGetProject projectB = CreateNewMSBuildProject();
            msbuildNuGetProjects = new List<NuGetProject>() { projectA, projectB };
        }

        private NuGetProject CreateNewMSBuildProject()
        {
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework);
            NuGetProject project = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);
            return project;
        }

        public NuGetProject DefaultNuGetProject
        {
            get
            {
                return msbuildNuGetProjects.FirstOrDefault();
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
            return msbuildNuGetProjects.
                Where(p => string.Equals(nuGetProjectSafeName, p.GetMetadata<string>(NuGetProjectMetadataKeys.Name), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            return nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
        }

        public IEnumerable<NuGetProject> GetProjects()
        {
            return msbuildNuGetProjects;
        }

        public bool IsSolutionOpen
        {
            get
            {
                bool isOpen = msbuildNuGetProjects != null;
                return isOpen;
            }
        }

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public string SolutionDirectory
        {
            get { return TestFilesystemUtility.CreateRandomTestFolder(); }
        }

        public event EventHandler SolutionOpened;
    }
}
