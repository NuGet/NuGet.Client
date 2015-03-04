using NuGet.Protocol.Core.Types;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGet.Protocol.Core.v2
{
    public static class FactoryExtensionsV2
    {
        public static SourceRepository GetCoreV2(this Repository.RepositoryFactory factory, string source)
        {
            return Repository.CreateSource(Repository.Provider.GetCoreV2(), source);
        }

        public static SourceRepository GetCoreV2(this Repository.RepositoryFactory factory, Configuration.PackageSource source)
        {
            return Repository.CreateSource(Repository.Provider.GetCoreV2(), source);
        }

        public static IEnumerable<Lazy<INuGetResourceProvider>> GetCoreV2(this Repository.ProviderFactory factory)
        {
            yield return new Lazy<INuGetResourceProvider>(() => new DependencyInfoResourceV2Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new DownloadResourceV2Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new MetadataResourceV2Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new PackageRepositoryResourceV2Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new SearchLatestResourceV2Provider());
            yield return new Lazy<INuGetResourceProvider>(() => new SimpleSearchResourceV2Provider());

            yield break;
        }
    }
}