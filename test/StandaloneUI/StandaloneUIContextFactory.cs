using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace StandaloneUI
{
    public class StandaloneUIContextFactory
    {
        private readonly ISourceRepositoryProvider _repositoryProvider;

        // TODO: add this one it is implemented
        private readonly ISolutionManager _solutionManager;

        private readonly IPackageRestoreManager _restoreManager;
        private readonly IOptionsPageActivator _optionsPage;
        private readonly ISettings _settings;

        public StandaloneUIContextFactory(ISourceRepositoryProvider repositoryProvider,
            ISolutionManager solutionManager,
            ISettings settings,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPage)
        {
            _repositoryProvider = repositoryProvider;
            _solutionManager = solutionManager;
            _settings = settings;
            _restoreManager = packageRestoreManager;
            _optionsPage = optionsPage;
        }

        public INuGetUIContext Create(string settingsFile, IEnumerable<NuGetProject> projects)
        {
            if (String.IsNullOrEmpty(settingsFile))
            {
                throw new ArgumentException("settingsFile");
            }

            if (projects == null || !projects.Any())
            {
                throw new ArgumentNullException("projects");
            }

            NuGetPackageManager packageManager = new NuGetPackageManager(_repositoryProvider, _settings, _solutionManager);
            UIActionEngine actionEngine = new UIActionEngine(_repositoryProvider, packageManager);

            return new StandaloneUIContext(settingsFile, _repositoryProvider, _solutionManager, packageManager, actionEngine, _restoreManager, _optionsPage, projects);
        }
    }
}