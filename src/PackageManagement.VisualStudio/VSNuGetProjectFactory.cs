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

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSNuGetProjectFactory
    {
        private ISolutionManager SolutionManager { get; set; }
        private ISettings Settings { get; set; }
        private EmptyNuGetProjectContext EmptyNuGetProjectContext { get; set; }

        // TODO: Add ISettings, ISolutionManager, IDeleteOnRestartManager, VsPackageInstallerEvents and IVsFrameworkMultiTargeting to constructor
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
            if(nuGetProjectContext == null)
            {
                nuGetProjectContext = EmptyNuGetProjectContext;
            }

            var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(envDTEProject, nuGetProjectContext);
            var folderNuGetProjectFullPath = PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings);
            // TODO: Handle non-default packages.config name
            var packagesConfigFullPath = Path.Combine(EnvDTEProjectUtility.GetFullPath(envDTEProject), "packages.config");
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, folderNuGetProjectFullPath, packagesConfigFullPath);

            return msBuildNuGetProject;
        }
    }
}
