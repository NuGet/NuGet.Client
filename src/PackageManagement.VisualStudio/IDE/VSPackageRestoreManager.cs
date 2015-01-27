using NuGet.Client;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IPackageRestoreManager))]
    internal class VSPackageRestoreManager : PackageRestoreManager
    {
        public VSPackageRestoreManager()
            : this(
                ServiceLocator.GetInstance<ISourceRepositoryProvider>(),
                ServiceLocator.GetInstance<ISettings>(),
                ServiceLocator.GetInstance<ISolutionManager>())
        {
        }

        public VSPackageRestoreManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
            : base(sourceRepositoryProvider, settings, solutionManager)
        {
            SolutionManager = solutionManager;
            SolutionManager.NuGetProjectAdded += OnNuGetProjectAdded;
            SolutionManager.SolutionOpened += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;
        }

        private ISolutionManager SolutionManager { get; set; }

        public void EnableCurrentSolutionForRestore(bool fromActivation)
        {
            throw new NotImplementedException();
        }

        public bool IsCurrentSolutionEnabledForRestore
        {
            get { return false; }
        }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            // We need to do the check even on Solution Closed because, let's say if the yellow Update bar
            // is showing and the user closes the solution; in that case, we want to hide the Update bar.
            base.RaisePackagesMissingEventForSolution();
        }

        private void OnNuGetProjectAdded(object sender, NuGetProjectEventArgs e)
        {
            if (IsCurrentSolutionEnabledForRestore)
            {
                throw new NotImplementedException();
                //EnablePackageRestore(e.Project, _packageManagerFactory.CreatePackageManager());
            }

            base.RaisePackagesMissingEventForSolution();
        }
    }
}
