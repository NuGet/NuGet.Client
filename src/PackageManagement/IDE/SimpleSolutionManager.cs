using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class SimpleSolutionManager : ISolutionManager
    {
        public event EventHandler SolutionOpening;
        public event EventHandler SolutionOpened;

        public event EventHandler SolutionClosing;

        public event EventHandler SolutionClosed;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;
        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;
        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;

        protected Dictionary<string, NuGetProject> NuGetProjects { get; set; }

        public SimpleSolutionManager(string solutionDirectory, INuGetProjectContext nuGetProjectContext = null)
        {
            SolutionDirectory = solutionDirectory;
            NuGetProjects = new Dictionary<string, NuGetProject>();
            NuGetProjectContext = nuGetProjectContext ?? new EmptyNuGetProjectContext();
        }

        public string SolutionDirectory
        {
            get;
            protected set;
        }

        public string DefaultNuGetProjectName
        {
            get;
            set;
        }

        public NuGetProject DefaultNuGetProject
        {
            get
            {
                return GetNuGetProject(DefaultNuGetProjectName);
            }
        }

        public bool IsSolutionOpen
        {
            get { return true; }
        }

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            return NuGetProjects.Values;
        }

        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            return GetName(nuGetProject);
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            if (NuGetProjects.ContainsKey(nuGetProjectSafeName))
            {
                return NuGetProjects[nuGetProjectSafeName];
            }
            throw new ArgumentException(String.Format(Strings.NoNuGetProjectWithSpecifiedName, nuGetProjectSafeName));
        }

        public void AddNewNuGetProject(NuGetProject nuGetProject)
        {
            string nuGetProjectName = GetName(nuGetProject);
            if (NuGetProjects.ContainsKey(nuGetProjectName))
            {
                throw new ArgumentException(String.Format(Strings.AnotherNuGetProjectWithSameNameExistsInSolution, nuGetProjectName));
            }
            NuGetProjects.Add(nuGetProjectName, nuGetProject);
        }

        public bool RemoveNuGetProject(string nuGetProjectSafeName)
        {
            if(NuGetProjects.ContainsKey(nuGetProjectSafeName))
            {
                NuGetProjects.Remove(nuGetProjectSafeName);
                return true;
            }

            return false;
        }

        private string GetName(NuGetProject nuGetProject)
        {
            string nuGetProjectName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            if(!String.IsNullOrEmpty(nuGetProjectName))
            {
                throw new ArgumentException(String.Format(Strings.NuGetProjectDoesNotHaveName, nuGetProjectName));
            }

            return nuGetProjectName;
        }

        public INuGetProjectContext NuGetProjectContext
        {
            get;
            set;
        }
    }
}
