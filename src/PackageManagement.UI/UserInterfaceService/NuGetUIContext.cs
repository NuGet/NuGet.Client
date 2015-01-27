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
    /// <summary>
    /// Context of a PackageManagement UI window
    /// </summary>
    public class NuGetUIContext : INuGetUIContext
    {
        private readonly ISourceRepositoryProvider _sourceProvider;
        private readonly ISolutionManager _solutionManager;
        private readonly NuGetPackageManager _packageManager;
        private readonly UIActionEngine _uiActionEngine;
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly IOptionsPageActivator _optionsPageActivator;
        private readonly NuGetProject[] _projects;

        public NuGetUIContext(
            ISourceRepositoryProvider sourceProvider, 
            ISolutionManager solutionManager, 
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IEnumerable<NuGetProject> projects)
        {
            _sourceProvider = sourceProvider;
            _solutionManager = solutionManager;
            _packageManager = packageManager;
            _uiActionEngine = uiActionEngine;
            _packageManager = packageManager;
            _packageRestoreManager = packageRestoreManager;
            _optionsPageActivator = optionsPageActivator;
            _projects = projects.ToArray();
        }

        public ISourceRepositoryProvider SourceProvider
        {
            get
            {
                return _sourceProvider;
            }
        }

        public ISolutionManager SolutionManager
        {
            get
            {
                return _solutionManager;
            }
        }

        public NuGetPackageManager PackageManager
        {
            get
            {
                return _packageManager;
            }
        }

        public UIActionEngine UIActionEngine
        {
            get
            {
                return _uiActionEngine;
            }
        }

        public IPackageRestoreManager PackageRestoreManager
        {
            get
            {
                return _packageRestoreManager;
            }
        }

        public IOptionsPageActivator OptionsPageActivator
        {
            get
            {
                return _optionsPageActivator;
            }
        }


        public IEnumerable<NuGetProject> Projects
        {
            get
            {
                return _projects;
            }
        }
    }
}
