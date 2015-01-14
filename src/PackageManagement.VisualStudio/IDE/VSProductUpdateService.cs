using System;
using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IProductUpdateService))]
    internal class VSProductUpdateService : IProductUpdateService
    {
        public void CheckForAvailableUpdateAsync()
        {
            throw new NotImplementedException();
        }

        public void DeclineUpdate(bool doNotRemindAgain)
        {
            throw new NotImplementedException();
        }

        public void Update()
        {
            throw new NotImplementedException();
        }

        public event EventHandler<ProductUpdateAvailableEventArgs> UpdateAvailable;
    }
}
