using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProperty = EnvDTE.Property;

namespace NuGet.ProjectManagement.VisualStudio
{
    public class VSNuGetProjectFactory
    {
        IDictionary<string, NuGetProject> VSNuGetProjects = new Dictionary<string, NuGetProject>();

        // TODO: Add ISettings, ISolutionManager, IDeleteOnRestartManager, VsPackageInstallerEvents and IVsFrameworkMultiTargeting to constructor
        public VSNuGetProjectFactory()
        {
        }

        private ISettings Settings { get; set;}

        public NuGetProject GetNuGetProject(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            var envDTEProjectUniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);
            NuGetProject nuGetProject = null;
            if(!VSNuGetProjects.TryGetValue(envDTEProjectUniqueName, out nuGetProject))
            {
                nuGetProject = CreateNuGetProject(envDTEProject, nuGetProjectContext);
                VSNuGetProjects[envDTEProjectUniqueName] = nuGetProject;
            }
            return nuGetProject;
        }

        private NuGetProject CreateNuGetProject(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(envDTEProject, nuGetProjectContext);
            var folderNuGetProjectFullPath = GetPackagesDirectoryFullPath(envDTEProject);
            // TODO: Handle non-default packages.config name
            var packagesConfigFullPath = Path.Combine(EnvDTEProjectUtility.GetFullPath(envDTEProject), "packages.config");
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, folderNuGetProjectFullPath, packagesConfigFullPath);

            return msBuildNuGetProject;
        }

        private string GetPackagesDirectoryFullPath(EnvDTEProject envDTEProject)
        {
            string solutionFilePath = GetSolutionFilePath(envDTEProject.DTE);
            string repositoryPath = GetRepositoryPath();
            return Path.Combine(Path.GetDirectoryName(solutionFilePath), repositoryPath);
        }

        private string GetRepositoryPath()
        {
            // TODO: Change this to get the 'repositoryPath' from settings
            return "packages";
        }

        private string GetSolutionFilePath(EnvDTE.DTE dte)
        {
            // Use .Properties.Item("Path") instead of .FullName because .FullName might not be
            // available if the solution is just being created
            string solutionFilePath = null;

            EnvDTEProperty property = dte.Solution.Properties.Item("Path");
            if (property == null)
            {
                return null;
            }
            try
            {
                // When using a temporary solution, (such as by saying File -> New File), querying this value throws.
                // Since we wouldn't be able to do manage any packages at this point, we return null. Consumers of this property typically 
                // use a String.IsNullOrEmpty check either way, so it's alright.
                solutionFilePath = (string)property.Value;
            }
            catch (COMException)
            {
                return null;
            }

            return solutionFilePath;
        }
    }
}
