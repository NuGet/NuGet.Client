using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Allows an IPackageRepository repository to be passed in to a SourceRepository
    /// </summary>
    public class V2PackageSource : Configuration.PackageSource
    {
        private Func<IPackageRepository> _createRepo;

        public V2PackageSource(string source, Func<IPackageRepository> createRepo)
            : base(source)
        {
            _createRepo = createRepo;
        }

        public IPackageRepository CreatePackageRepository()
        {
            return _createRepo();
        }
    }
}
