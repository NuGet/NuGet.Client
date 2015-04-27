using NuGet.Protocol.Core.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.ApiApps
{
    public static class FactoryExtensionsApiApp
    {
        public static SourceRepository GetApiApps(this Repository.RepositoryFactory factory, string source)
        {
            return Repository.CreateSource(Repository.Provider.GetApiApps(), source);
        }

        public static SourceRepository GetApiApps(this Repository.RepositoryFactory factory, Configuration.PackageSource source)
        {
            return Repository.CreateSource(Repository.Provider.GetApiApps(), source);
        }

        /// <summary>
        /// Core V3 + ApiApps
        /// </summary>
        public static IEnumerable<Lazy<INuGetResourceProvider>> GetApiApps(this Repository.ProviderFactory factory)
        {
            yield return new Lazy<INuGetResourceProvider>(() => new ApiAppSearchResourceProvider());
            yield return new Lazy<INuGetResourceProvider>(() => new ServiceIndexResourceV3Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new MetadataResourceV3Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new MetadataResourceV3Provider());

            yield break;
        }
    }
}