using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;

namespace Test.Utility
{
    public class TestSourceRepositoryUtility
    {
        public TestSourceRepositoryUtility() {}

        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] ResourceProviders;

        private CompositionContainer Initialize()
        {
            var aggregateCatalog = new AggregateCatalog();
            {
                aggregateCatalog.Catalogs.Add(new DirectoryCatalog(Environment.CurrentDirectory, "*.dll"));
                var container = new CompositionContainer(aggregateCatalog);
                container.ComposeParts(this);
                return container;
            }
        }

        public static SourceRepositoryProvider CreateV3OnlySourceRepositoryProvider()
        {
            var thisUtility = new TestSourceRepositoryUtility();
            var container = thisUtility.Initialize();
            var packageSourceProvider = new V3OnlyPackageSourceProvider();

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, thisUtility.ResourceProviders);
            return sourceRepositoryProvider;
        }
    }

    /// <summary>
    /// Provider that only returns V3 as a source
    /// </summary>
    class V3OnlyPackageSourceProvider : IPackageSourceProvider
    {

        public void DisablePackageSource(PackageSource source)
        {
            throw new NotImplementedException();
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            return true;
        }

        public IEnumerable<PackageSource> LoadPackageSources()
        {
            return new List<PackageSource>() { new PackageSource("https://az320820.vo.msecnd.net/ver3-preview/index.json", "v3") };
        }

        public event EventHandler PackageSourcesSaved;

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            throw new NotImplementedException();
        }
    }

}
