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
        private static PackageSource V2PackageSource = new PackageSource("https://www.nuget.org/api/v2", "v2");
        private static PackageSource V3PackageSource = new PackageSource("https://az320820.vo.msecnd.net/ver3-preview/index.json", "v3");
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
            return CreateSourceRepositoryProvider(new List<PackageSource>() { V3PackageSource });
        }

        public static SourceRepositoryProvider CreateV2OnlySourceRepositoryProvider()
        {
            return CreateSourceRepositoryProvider(new List<PackageSource>() { V2PackageSource });
        }

        public static SourceRepositoryProvider CreateSourceRepositoryProvider(IEnumerable<PackageSource> packageSources)
        {
            var thisUtility = new TestSourceRepositoryUtility();
            var container = thisUtility.Initialize();
            var packageSourceProvider = new TestPackageSourceProvider(packageSources);

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, thisUtility.ResourceProviders);
            return sourceRepositoryProvider;
        }
    }

    /// <summary>
    /// Provider that only returns V3 as a source
    /// </summary>
    class TestPackageSourceProvider : IPackageSourceProvider
    {
        private IEnumerable<PackageSource> PackageSources { get; set; }
        public TestPackageSourceProvider(IEnumerable<PackageSource> packageSources)
        {
            PackageSources = packageSources;
        }
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
            return PackageSources;
        }

        public event EventHandler PackageSourcesSaved;

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            throw new NotImplementedException();
        }
    }

}
