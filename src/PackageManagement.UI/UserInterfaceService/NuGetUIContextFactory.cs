using NuGet.Client;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    [Export(typeof(INuGetUIContextFactory))]
    public class NuGetUIContextFactory : INuGetUIContextFactory
    {
        private readonly ISourceRepositoryProvider _repositoryProvider;

        // TODO: add this one it is implemented
        private readonly ISolutionManager _solutionManager;
        private readonly IPackageRestoreManager _restoreManager;
        private readonly IOptionsPageActivator _optionsPage;
        private readonly ISettings _settings;

        /// <summary>
        /// Non-MEF constructor
        /// </summary>
        /// <param name="repositoryProvider"></param>
        public NuGetUIContextFactory(SourceRepositoryProvider repositoryProvider)
        {
            _repositoryProvider = repositoryProvider;
        }

        [ImportingConstructor]
        public NuGetUIContextFactory([Import]ISourceRepositoryProvider repositoryProvider, [Import]ISolutionManager solutionManager,
            [Import]ISettings settings, [Import]IPackageRestoreManager packageRestoreManager)
        {
            _repositoryProvider = repositoryProvider;
            _solutionManager = solutionManager;
            _settings = settings;
            _restoreManager = packageRestoreManager;
        }

        public INuGetUIContext Create(IEnumerable<NuGetProject> projects)
        {
            if (projects == null || !projects.Any())
            {
                throw new ArgumentNullException("projects");
            }

            NuGetPackageManager packageManager = new NuGetPackageManager(_repositoryProvider, _settings, _solutionManager);
            UIActionEngine actionEngine = new UIActionEngine(_repositoryProvider, packageManager);

            return new NuGetUIContext(_repositoryProvider, _solutionManager, packageManager, actionEngine, _restoreManager, _optionsPage, projects);
        }
    }
}
