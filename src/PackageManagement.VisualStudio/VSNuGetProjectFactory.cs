using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSNuGetProjectFactory
    {
        private readonly Func<string> _packagesPath;

        private EmptyNuGetProjectContext EmptyNuGetProjectContext { get; set; }

        // TODO: Add IDeleteOnRestartManager, VsPackageInstallerEvents and IVsFrameworkMultiTargeting to constructor
        public VSNuGetProjectFactory(Func<string> packagesPath)
        {
            if (packagesPath == null)
            {
                throw new ArgumentNullException("packagesPath");
            }

            _packagesPath = packagesPath;
            EmptyNuGetProjectContext = new EmptyNuGetProjectContext();
        }

        public NuGetProject CreateNuGetProject(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (nuGetProjectContext == null)
            {
                nuGetProjectContext = EmptyNuGetProjectContext;
            }

            var projectK = GetProjectKProject(envDTEProject);
            if (projectK != null)
            {
                return new ProjectKNuGetProject(projectK, envDTEProject.Name, envDTEProject.UniqueName);
            }

            var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(envDTEProject, nuGetProjectContext);
            var folderNuGetProjectFullPath = _packagesPath();

            var packagesConfigFullPath = EnvDTEProjectUtility.GetPackagesConfigFullPath(envDTEProject);
            var packagesConfigWithProjectNameFullPath = EnvDTEProjectUtility.GetPackagesConfigWithProjectNameFullPath(envDTEProject);

            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, folderNuGetProjectFullPath,
                File.Exists(packagesConfigWithProjectNameFullPath) ? packagesConfigWithProjectNameFullPath : packagesConfigFullPath);

            return msBuildNuGetProject;
        }

        public static INuGetPackageManager GetProjectKProject(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

                if (!(retValue is INuGetPackageManager))
                {
                    // Workaround a bug in Dev14 prereleases where Lazy<INuGetPackageManager> was returned.
                    var properties = retValue.GetType().GetProperties().Where(p => p.Name == "Value");
                    if (properties.Count() == 1)
                    {
                        retValue = properties.First().GetValue(retValue);
                    }
                }

                return retValue as INuGetPackageManager;
            }
        }
    }
}
