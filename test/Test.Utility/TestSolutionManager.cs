using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestSolutionManager : ISolutionManager
    {
        private List<NuGetProject> NuGetProjects { get; set; }

        public string SolutionDirectory { get; private set; }

        private const string PackagesFolder = "packages";

        public TestSolutionManager(string solutionDirectory = null)
        {
            SolutionDirectory = String.IsNullOrEmpty(solutionDirectory) ? TestFilesystemUtility.CreateRandomTestFolder() : solutionDirectory;
            NuGetProjects = new List<NuGetProject>();
            NuGetProjectContext = new TestNuGetProjectContext();
        }

        public NuGetProject AddNewMSBuildProject(string projectName = null, NuGetFramework projectTargetFramework = null, string packagesConfigName = null)
        {
            if (GetNuGetProject(projectName) != null)
            {
                throw new ArgumentException("Project with " + projectName + " already exists");
            }

            var packagesFolder = Path.Combine(SolutionDirectory, PackagesFolder);
            projectName = String.IsNullOrEmpty(projectName) ? Guid.NewGuid().ToString() : projectName;
            var projectFullPath = Path.Combine(SolutionDirectory, projectName);
            Directory.CreateDirectory(projectFullPath);

            projectTargetFramework = projectTargetFramework ?? NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext(),
                projectFullPath, projectName);
            NuGetProject nuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolder, projectFullPath);
            NuGetProjects.Add(nuGetProject);
            return nuGetProject;
        }

        //public NuGetProject AddProjectKProject(string projectName)
        //{
        //    var testProjectKProject = new TestProjectKProject();
        //    var nugetProject = new ProjectKNuGetProjectBase(testProjectKProject, projectName);
        //    NuGetProjects.Add(nugetProject);
        //    return nugetProject;
        //}

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

        public INuGetProjectContext NuGetProjectContext
        {
            get;
            set;
        }

#pragma warning disable 0067
        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public event EventHandler SolutionOpened;
        public event EventHandler SolutionOpening;

#pragma warning restore 0067
    }
}