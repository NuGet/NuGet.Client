using NuGet.Client;
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
        private readonly SourceRepositoryProvider _repositoryProvider;

        // TODO: add this one it is implemented
        private readonly ISolutionManager _solutionManager;
        private readonly IPackageRestoreManager _restoreManager;
        private readonly IOptionsPageActivator _optionsPage;

        [ImportingConstructor]
        public NuGetUIContextFactory(SourceRepositoryProvider repositoryProvider)
        {
            _repositoryProvider = repositoryProvider;
        }

        public INuGetUIContext Create(IEnumerable<NuGetProject> projects)
        {
            if (projects == null || !projects.Any())
            {
                throw new ArgumentNullException("projects");
            }

            NuGetPackageManager packageManager = new NuGetPackageManager(_repositoryProvider);
            UIActionEngine actionEngine = new UIActionEngine(_repositoryProvider, packageManager);

            return new NuGetUIContext(_repositoryProvider, _solutionManager, packageManager, actionEngine, _restoreManager, _optionsPage, projects);
        }
    }
}
