using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IPackageRestoreManager))]
    internal class VSPackageRestoreManager : IPackageRestoreManager
    {
        public void CheckForMissingPackages()
        {
            throw new NotImplementedException();
        }

        public void EnableCurrentSolutionForRestore(bool fromActivation)
        {
            throw new NotImplementedException();
        }

        public bool IsCurrentSolutionEnabledForRestore
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        public Task RestoreMissingPackages()
        {
            throw new NotImplementedException();
        }
    }
}
