using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface IPackageSourceProvider
    {
        IEnumerable<PackageSource> LoadPackageSources();

        event EventHandler PackageSourcesChanged;        

        void SavePackageSources(IEnumerable<PackageSource> sources);
        void DisablePackageSource(PackageSource source);
        bool IsPackageSourceEnabled(PackageSource source);

        string ActivePackageSourceName { get; }

        void SaveActivePackageSource(PackageSource source);
    }
}
