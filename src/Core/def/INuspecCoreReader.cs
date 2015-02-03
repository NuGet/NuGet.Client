using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// A basic nuspec reader that understands the id, version, and min client version of a package.
    /// </summary>
    public interface INuspecCoreReader
    {
        /// <summary>
        /// Package Id
        /// </summary>
        /// <returns></returns>
        string GetId();

        /// <summary>
        /// Package Version
        /// </summary>
        NuGetVersion GetVersion();

        /// <summary>
        /// Minimum client version needed to consume the package.
        /// </summary>
        SemanticVersion GetMinClientVersion();

        /// <summary>
        /// The locale ID for the package, such as en-us.
        /// </summary>
        string GetLanguage();

        /// <summary>
        /// Id and Version of a package.
        /// </summary>
        PackageIdentity GetIdentity();

        /// <summary>
        /// Package metadata in the nuspec
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> GetMetadata();
    }
}
