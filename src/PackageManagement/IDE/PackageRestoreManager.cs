using NuGet.Configuration;
using System;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class PackageRestoreManager : IPackageRestoreManager
    {
        public SourceRepositoryProvider SourceRepositoryProvider { get; private set; }
        public ISolutionManager SolutionManager { get; private set; }
        public ISettings Settings { get; private set; }
        public PackageRestoreManager(SourceRepositoryProvider sourceRepositoryProvider, ISolutionManager solutionManager, ISettings settings)
        {
            if(sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException();
            }
        }
        public bool IsCurrentSolutionEnabledForRestore
        {
            get { throw new NotImplementedException(); }
        }

        public void EnableCurrentSolutionForRestore(bool fromActivation)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        public void CheckForMissingPackages()
        {
            throw new NotImplementedException();
        }

        public Task RestoreMissingPackages()
        {
            throw new NotImplementedException();
        }
    }
}
