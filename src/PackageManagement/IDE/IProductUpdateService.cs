using System;

namespace NuGet.PackageManagement
{
    public interface IProductUpdateService
    {
        void CheckForAvailableUpdateAsync();
        void Update();
        void DeclineUpdate(bool doNotRemindAgain);
        event EventHandler<ProductUpdateAvailableEventArgs> UpdateAvailable;
    }

    public class ProductUpdateAvailableEventArgs : EventArgs
    {
        internal ProductUpdateAvailableEventArgs(Version currentVersion, Version newVersion)
        {
            CurrentVersion = currentVersion;
            NewVersion = newVersion;
        }

        public Version CurrentVersion { get; private set; }
        public Version NewVersion { get; private set; }
    }
}