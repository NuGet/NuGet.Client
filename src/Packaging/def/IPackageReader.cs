using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public interface IPackageReader : IDisposable
    {
        PackageIdentity GetIdentity();

        Stream GetNuspec();

        Stream GetPackedManifest();

        ComponentTree GetComponentTree();

        IFileSystem FileSystem { get; }
    }
}
