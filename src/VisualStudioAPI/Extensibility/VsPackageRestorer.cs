extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using EnvDTE;
using NuGetConsole;
using System.Linq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.PackageManagement;
using NuGet.Client;
using System.Threading;

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
            NuGetPackageManager packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);

            try
            {
                var task = System.Threading.Tasks.Task.Run(async () => await _restoreManager.RestoreMissingPackagesInSolutionAsync(CancellationToken.None));
                task.Wait();
            }
            catch (Exception ex)
            {
                ExceptionHelper.WriteToActivityLog(ex);
            }
        }
    }
}
