using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

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

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                NuGetPackageManager packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);


                UninstallationContext uninstallContext = new UninstallationContext(removeDependencies, false);
                VSAPIProjectContext projectContext = new VSAPIProjectContext();

                // find the project
                NuGetProject nuGetProject = PackageManagementHelpers.GetProject(_solutionManager, project, projectContext);

                // uninstall the package
                await packageManager.UninstallPackageAsync(nuGetProject, packageId, uninstallContext, projectContext, CancellationToken.None);
            });
        }
    }
}
