using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public interface IPackageReaderCore : IDisposable
    {
        PackageIdentity GetIdentity();

        ComponentTree GetComponentTree();
    }
}
