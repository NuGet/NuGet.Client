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
            // TODO: add this after NuGet.Configuration has added this class
            throw new NotImplementedException();

            //var settings = ServiceLocator.GetInstance<ISettings>();
            //var packageRestoreConsent = new PackageRestoreConsent(settings);
            //return packageRestoreConsent.IsGranted;
        }

        public void RestorePackages(Project project)
        {
            NuGetPackageManager packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);

            _restoreManager.RestoreMissingPackagesInSolution().Wait();
        }
    }
}
