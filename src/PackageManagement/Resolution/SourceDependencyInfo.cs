using NuGet.Client;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    // TODO: make this internal

    /// <summary>
    /// Source aware PackageDependencyInfo for multi repo scenarios.
    /// </summary>
    public class SourceDependencyInfo : PackageDependencyInfo
    {
        public SourceRepository Source { get; private set; }

        public SourceDependencyInfo(PackageDependencyInfo info, SourceRepository source)
            : base(info, info.Dependencies)
        {
            Source = source;
        }
    }
}
