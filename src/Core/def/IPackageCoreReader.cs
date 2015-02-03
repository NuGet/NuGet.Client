using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// Basic package reader
    /// </summary>
    public interface IPackageReaderCore : IDisposable
    {
        /// <summary>
        /// Identity of the package
        /// </summary>
        /// <returns></returns>
        PackageIdentity GetIdentity();

        /// <summary>
        /// Minimum client version needed to consume the package.
        /// </summary>
        SemanticVersion GetMinClientVersion();

        // TODO: add dependency info
    }
}
