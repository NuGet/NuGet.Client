using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using NuGet.Client;
using NuGet.Client.VisualStudio.Models;
using System.Diagnostics;
using System.Runtime.Versioning;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;
using Xunit;

namespace V2V3ResourcesTest
{
    public class V2V3ResourcesTest
    {
        private CompositionContainer container;
        private string V3SourceUrl = "https://az320820.vo.msecnd.net/ver3-preview/index.json";
        private string V2SourceUrl = "https://nuget.org";
        public V2V3ResourcesTest()
        {
            try
            {
                //Creating an instance of aggregate catalog. It aggregates other catalogs
                var aggregateCatalog = new AggregateCatalog();
                //Build the directory path where the parts will be available
                var directoryPath = Environment.CurrentDirectory;
                var directoryCatalog = new DirectoryCatalog(directoryPath, "*NuGet.Client.V3*.dll");
                aggregateCatalog.Catalogs.Add(directoryCatalog);
                container = new CompositionContainer(aggregateCatalog);
                container.ComposeParts(this);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [Theory]
        [InlineData("https://nuget.org")]
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestDownloadResource(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IDownload resource = (IDownload)repo.GetResource<IDownload>().Result;
            Assert.True(resource != null);
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IDownload)));            
            PackageDownloadMetadata downloadMetadata = resource.GetNupkgUrlForDownload(new PackageIdentity("jQuery", new NuGetVersion("1.6.4"))).Result;
            //*TODOs: Check if the download Url ends with .nupkg. More detailed verification can be added to see if the nupkg file can be fetched from the location.
            Assert.True(downloadMetadata.NupkgDownloadUrl.OriginalString.EndsWith(".nupkg")); 
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestMetadataResource(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IMetadata resource = (IMetadata)repo.GetResource<IMetadata>().Result;
            Assert.True(resource != null);
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IMetadata)));
            NuGetVersion latestVersion = resource.GetLatestVersion("Nuget.core").Result;
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion.ToNormalizedString().Contains("2.8.3")); 
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestVisualStudioUIMetadataResource(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IVisualStudioUIMetadata resource = (IVisualStudioUIMetadata)repo.GetResource<IVisualStudioUIMetadata>().Result;
            Assert.True(resource != null);
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IVisualStudioUIMetadata)));
            //*TODOs: Use a proper test package whose metatdata and versions are fixed instead of exisitng packages.
            VisualStudioUIPackageMetadata packageMetadata = resource.GetPackageMetadataForVisualStudioUI("Microsoft.AspNet.Razor", new NuGetVersion("4.0.0-beta1")).Result;
            Assert.True(packageMetadata.HasDependencies.Equals(true)); 
            Assert.True(packageMetadata.DependencySets.Count() == 1);
            Assert.True(packageMetadata.DependencySets.First().Dependencies.Count().Equals(12));

            IEnumerable<VisualStudioUIPackageMetadata> packageMetadataList = resource.GetPackageMetadataForAllVersionsForVisualStudioUI("Nuget.core").Result;
            Assert.True(packageMetadataList != null);
            Assert.True(packageMetadataList.Count() == 46);
            
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestVisualStudioUISearchResource(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IVisualStudioUISearch resource = (IVisualStudioUISearch)repo.GetResource<IVisualStudioUISearch>().Result;
            //Check if we are able to obtain a resource
            Assert.True(resource != null);
            //check if the resource is of type IVsSearch.
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IVisualStudioUISearch))); 
            SearchFilter filter = new SearchFilter(); //create a dummy filter.
            List<string> fxNames = new List<string>();
            fxNames.Add(new FrameworkName(".NET Framework, Version=4.0").FullName);
            filter.SupportedFrameworks = fxNames;
            IEnumerable<VisualStudioUISearchMetadata> searchResults = resource.GetSearchResultsForVisualStudioUI("Elmah", filter, 0, 100, new System.Threading.CancellationToken()).Result;
            // Check if non empty search result is returned.
            Assert.True(searchResults.Count() > 0);
            //check if there is atleast one result which has Elmah as title.
            Assert.True(searchResults.Any(p => p.Id.Equals("Elmah", StringComparison.OrdinalIgnoreCase))); 
        }

        [Theory]
        [InlineData("https://nuget.org")]
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestPowerShellAutocompleteResourceForPackageIds(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IPowerShellAutoComplete resource = (IPowerShellAutoComplete)repo.GetResource<IPowerShellAutoComplete>().Result;
            //Check if we are able to obtain a resource
            Assert.True(resource != null); 
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IPowerShellAutoComplete))); //check if the resource is of type IVsSearch.     
            IEnumerable<string> searchResults = resource.GetPackageIdsStartingWith("Elmah", new System.Threading.CancellationToken()).Result;
            // Check if non empty search result is returned.
            Assert.True(searchResults.Count() > 0); 
            //Make sure that all the package ids contains the search term in it.
            Assert.True(!searchResults.Any(p => p.IndexOf("Elmah",StringComparison.OrdinalIgnoreCase) == -1)); 
        }

        [Theory]       
        [InlineData("https://az320820.vo.msecnd.net/ver3-preview/index.json")]
        public void TestPowerShellAutocompleteResourceForPackageVersions(string SourceUrl)
        {
            SourceRepository2 repo = GetSourceRepository(SourceUrl);
            IPowerShellAutoComplete resource = (IPowerShellAutoComplete)repo.GetResource<IPowerShellAutoComplete>().Result;
            //Check if we are able to obtain a resource
            Assert.True(resource != null);
            Assert.True(resource.GetType().GetInterfaces().Contains(typeof(IPowerShellAutoComplete))); //check if the resource is of type IVsSearch.     
            // Check if non zero version count is returned. *TODOS : Use a standard test packages whose version count will be fixed
            IEnumerable<NuGetVersion> versions = resource.GetAllVersions("jQuery").Result ;            
            Assert.True(versions.Count() >= 35);
        }

        #region PrivateHelpers
        private SourceRepository2 GetSourceRepository(string SourceUrl)
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Assert.True(providers.Count() > 0);
            PackageSource source = new PackageSource("Source", SourceUrl);
            SourceRepository2 repo = new SourceRepository2(source, providers);
            return repo;
        }
        #endregion PrivateHelpers


    }
}
