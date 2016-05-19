using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet
{
    public interface IPackageRepository
    {
        string Source { get; }
        PackageSaveModes PackageSaveMode { get; set; }
        bool SupportsPrereleasePackages { get; }
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This call might be expensive")]
        IQueryable<IPackage> GetPackages();

        // Which files (nuspec/nupkg) are saved is controlled by property PackageSaveMode.
        void AddPackage(IPackage package);
        void RemovePackage(IPackage package);
    }
}
