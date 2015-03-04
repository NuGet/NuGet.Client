using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Holds an IPackageRepository
    /// </summary>
    public class PackageRepositoryResourceV2 : V2Resource
    {

        public PackageRepositoryResourceV2(IPackageRepository repository)
            : base(repository)
        {

        }

    }
}
