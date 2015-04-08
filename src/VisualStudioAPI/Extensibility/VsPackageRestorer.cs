using System;
using System.ComponentModel.Composition;
using System.Threading;
using EnvDTE;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using Microsoft.VisualStudio.Shell;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageRestorer))]
    public class VsPackageRestorer : IVsPackageRestorer
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private ISettings _settings;
        private ISolutionManager _solutionManager;
        private IPackageRestoreManager _restoreManager;


        [ImportingConstructor]
        public VsPackageRestorer(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager, IPackageRestoreManager restoreManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _restoreManager = restoreManager;
        }

        public bool IsUserConsentGranted()
        {
            var packageRestoreConsent = new PackageRestoreConsent(_settings);
            return packageRestoreConsent.IsGranted;
        }

        public void RestorePackages(Project project)
        {
            try
            {
                var solutionDirectory = _solutionManager.SolutionDirectory;
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await _restoreManager.RestoreMissingPackagesInSolutionAsync(solutionDirectory, CancellationToken.None);
                });
            }
            catch (Exception ex)
            {
                ExceptionHelper.WriteToActivityLog(ex);
            }
        }
    }
}
