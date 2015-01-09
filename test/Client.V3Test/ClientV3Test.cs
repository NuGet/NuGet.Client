using NuGet.Client;
using NuGet.Configuration;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using NuGet.Client;
using Xunit;
//using Newtonsoft.Json.Linq;


namespace Client.V3Test
{
    public class ClientV3Test
    {
        private CompositionContainer container;

        //private string PreviewRootUrl = "https://az320820.vo.msecnd.net/ver3-preview/index.json";
        private string RCRootUrl = "https://nugetrcstage.blob.core.windows.net/ver3-rc/index.json";

        public ClientV3Test()
        {
            try
            {
                //Creating an instance of aggregate catalog. It aggregates other catalogs
                var aggregateCatalog = new AggregateCatalog();
                //Build the directory path where the parts will be available
                var directoryPath = Environment.CurrentDirectory;
                var directoryCatalog = new DirectoryCatalog(directoryPath, "*.dll");
                aggregateCatalog.Catalogs.Add(directoryCatalog);
                container = new CompositionContainer(aggregateCatalog);
                container.ComposeParts(this);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [Fact]
        public async Task TestLatestVersion()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.AlwaysPrerelease", true, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion.ToNormalizedString().Contains("5.0.0-beta"));
        }

        [Fact]
        public async Task TestLatestVersion2()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.AlwaysPrerelease", false, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion == null);
        }

        [Fact]
        public async Task TestLatestVersion3()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.MinClientVersion", false, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion.ToNormalizedString().Contains("1.0.0"));
        }

        [Fact]
        public async Task SimpleSearchTest()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            SimpleSearchResource resource = repo.GetResource<SimpleSearchResource>();
            Assert.True(resource != null);
            var results = await resource.Search("elmah", new SearchFilter(), 0, 10, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(results.Count() == 10);
        }

        #region PrivateHelpers
        private SourceRepository GetSourceRepository(string SourceUrl)
        {
            IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
            Assert.True(providers.Count() > 0);
            PackageSource source = new PackageSource(SourceUrl, "mysource", true);
            SourceRepository repo = new SourceRepository(source, providers);
            return repo;
        }
        #endregion PrivateHelpers
    }
}
