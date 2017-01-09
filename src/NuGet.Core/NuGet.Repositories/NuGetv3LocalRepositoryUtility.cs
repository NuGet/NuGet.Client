using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public static class NuGetv3LocalRepositoryUtility
    {
        /// <summary>
        /// Take the first match on id and version.
        /// </summary>
        public static LocalPackageSourceInfo GetPackage(
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            string id,
            NuGetVersion version)
        {
            LocalPackageInfo package = null;

            foreach (var repository in repositories)
            {
                package = repository.FindPackage(id, version);

                if (package != null)
                {
                    return new LocalPackageSourceInfo(repository, package);
                }
            }

            return null;
        }
    }
}
