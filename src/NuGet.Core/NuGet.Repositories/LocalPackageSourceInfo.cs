using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Repositories
{
    public class LocalPackageSourceInfo
    {
        public NuGetv3LocalRepository Repository { get; }

        public LocalPackageInfo Package { get; }

        public LocalPackageSourceInfo(NuGetv3LocalRepository repository, LocalPackageInfo package)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            Repository = repository;
            Package = package;
        }
    }
}
