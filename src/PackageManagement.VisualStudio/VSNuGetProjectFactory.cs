using NuGet.Configuration;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProperty = EnvDTE.Property;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSNuGetProjectFactory
    {
        private ISolutionManager SolutionManager { get; set; }
        private ISettings Settings { get; set; }
        private EmptyNuGetProjectContext EmptyNuGetProjectContext { get; set; }

        // TODO: Add IDeleteOnRestartManager, VsPackageInstallerEvents and IVsFrameworkMultiTargeting to constructor
        public VSNuGetProjectFactory(ISolutionManager solutionManager)
            : this(solutionManager, ServiceLocator.GetInstance<ISettings>()) { }

        public VSNuGetProjectFactory(ISolutionManager solutionManager, ISettings settings)
        {
            if(solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            if(settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            SolutionManager = solutionManager;
            Settings = settings;
            EmptyNuGetProjectContext = new EmptyNuGetProjectContext();
        }

        public NuGetProject CreateNuGetProject(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext = null)
        {
            if (nuGetProjectContext == null)
            {
                nuGetProjectContext = EmptyNuGetProjectContext;
            }

            var projectK = GetProjectKProject(envDTEProject);
            if (projectK != null)
            {
                return new ProjectKNuGetProject(projectK, envDTEProject.Name);
            }

            var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(envDTEProject, nuGetProjectContext);
            var folderNuGetProjectFullPath = PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings);
            var packagesConfigFiles = EnvDTEProjectUtility.GetPackageReferenceFileFullPaths(envDTEProject);

            // Item1 is path to "packages.config". Item2 is path to "packages.<projectName>.config"
            string packagesConfigFullPath = packagesConfigFiles.Item1;
            string packagesConfigWithProjectNameFullPath = packagesConfigFiles.Item2;

            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, folderNuGetProjectFullPath,
                File.Exists(packagesConfigWithProjectNameFullPath) ? packagesConfigWithProjectNameFullPath : packagesConfigFullPath);

            return msBuildNuGetProject;
        }

        public static INuGetPackageManager GetProjectKProject(EnvDTEProject project)
        {
            var vsProject = VsHierarchyUtility.ToVsHierarchy(project) as IVsProject;
            if (vsProject == null)
            {
                return null;
            }

            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider = null;
            vsProject.GetItemContext(
                (uint)Microsoft.VisualStudio.VSConstants.VSITEMID.Root,
                out serviceProvider);
            if (serviceProvider == null)
            {
                return null;
            }

            using (var sp = new ServiceProvider(serviceProvider))
            {
                var retValue = sp.GetService(typeof(INuGetPackageManager));
                if (retValue == null)
                {
                    return null;
                }

                var properties = retValue.GetType().GetProperties().Where(p => p.Name == "Value");
                if (properties.Count() != 1)
                {
                    return null;
                }

                var v = properties.First().GetValue(retValue) as INuGetPackageManager;
                return v as INuGetPackageManager;
            }
        }
    }
}
