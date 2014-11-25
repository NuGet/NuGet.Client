using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public interface IPackageReader : IPackageReaderCore
    {
        /// <summary>
        /// Returns all framework references found in the nuspec.
        /// </summary>
        IEnumerable<FrameworkSpecificGroup> GetFrameworkItems();

        /// <summary>
        /// Returns all items under the build folder.
        /// </summary>
        IEnumerable<FrameworkSpecificGroup> GetBuildItems();

        /// <summary>
        /// Returns all items under the tools folder.
        /// </summary>
        IEnumerable<FrameworkSpecificGroup> GetToolItems();

        /// <summary>
        /// Returns all items found in the content folder.
        /// </summary>
        /// <remarks>Some legacy behavior has been dropped here due to the mix of content folders and target framework folders here.</remarks>
        IEnumerable<FrameworkSpecificGroup> GetContentItems();

        /// <summary>
        /// Returns lib items + filtering based on the nuspec.
        /// </summary>
        IEnumerable<FrameworkSpecificGroup> GetLibItems();

        /// <summary>
        /// Returns package dependencies.
        /// </summary>
        IEnumerable<PackageDependencyGroup> GetPackageDependencies();
    }
}
