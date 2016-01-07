using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Packaging
{
    public static class PackageReaderExtensions
    {
        public static IEnumerable<string> GetPackageFiles(this IPackageCoreReader packageReader, PackageSaveModes packageSaveMode)
        {
            return packageReader.GetFiles().Where(file => PackageHelper.IsPackageFile(file, packageSaveMode));
        }
    }
}
