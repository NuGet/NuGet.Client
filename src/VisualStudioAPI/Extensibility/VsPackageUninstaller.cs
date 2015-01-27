using System;
using System.Linq;
using System.ComponentModel.Composition;
using System.Globalization;
using EnvDTE;
using NuGet.PackageManagement;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Client;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageUninstaller))]
    public class VsPackageUninstaller : IVsPackageUninstaller
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private ISettings _settings;
        private ISolutionManager _solutionManager;

        [ImportingConstructor]
        public VsPackageUninstaller(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
        }

        public void UninstallPackage(Project project, string packageId, bool removeDependencies)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId"));
            }

            try
            {
                NuGetPackageManager packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);

                // find the project
                NuGetProject nuGetProject = _solutionManager.GetNuGetProjects()
                    .Where(p => StringComparer.Ordinal.Equals(_solutionManager.GetNuGetProjectSafeName(p), project.UniqueName))
                    .SingleOrDefault();

                UninstallationContext uninstallContext = new UninstallationContext(removeDependencies, false);
                INuGetProjectContext projectContext = new VSAPIProjectContext();

                // uninstall the package
                packageManager.UninstallPackageAsync(nuGetProject, packageId, uninstallContext, projectContext).Wait();
            }
            finally
            {
                // TODO: log errors
            }
        }
    }
}
