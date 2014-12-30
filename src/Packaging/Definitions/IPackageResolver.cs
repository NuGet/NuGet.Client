using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public interface IPackageResolver
    {

        IEnumerable<PackageIdentity> Resolve(PackageIdentity toInstall, IEnumerable<PackageReference> installedPackages, IEnumerable<PackageIdentity> dependencyCandidates);

    }
}
