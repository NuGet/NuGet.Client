using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using NuGet.Client;
using System.Diagnostics;
using System.Runtime.Versioning;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;
using NuGet.Configuration;
using NuGet.PackagingCore;
using System.Threading;
using NuGet.Client.VisualStudio;
using NuGet;
using System.IO;

namespace V2V3ResourcesTest
{
    //*TODOs: Use a proper test package whose metatdata and versions are fixed instead ofusing exisitng packages.
    public class V2V3ResourcesTest
    {
        private CompositionContainer container;
        private string V2SourceUrl = "https://nuget.org/api/v2";
        public V2V3ResourcesTest()
        {
            try
            {
                //Creating an instance of aggregate catalog. It aggregates other catalogs
                var aggregateCatalog = new AggregateCatalog();
                //Build the directory path where the parts will be available
                var directoryPath = Environment.CurrentDirectory;
                var directoryCatalog = new DirectoryCatalog(directoryPath, "NuGet.Client*.dll");
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
        [InlineData("https://nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestDownloadResource(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            DownloadResource resource = repo.GetResource<DownloadResource>();
            Assert.True(resource != null);
            Uri downloadMetadata = await resource.GetDownloadUrl(new PackageIdentity("jQuery", new NuGetVersion("1.6.4")));
            //*TODOs: Check if the download Url ends with .nupkg. More detailed verification can be added to see if the nupkg file can be fetched from the location.
            Assert.True(downloadMetadata.OriginalString.EndsWith(".nupkg"));
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestMetadataResource(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("Nuget.core", true, true, CancellationToken.None);            
            Assert.True(latestVersion.ToNormalizedString().Contains("2.8.3"));
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestVisualStudioUIMetadataResource(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            UIMetadataResource resource = repo.GetResource<UIMetadataResource>();
            Assert.True(resource != null);   
            var result = await resource.GetMetadata("Microsoft.AspNet.Razor", true, true, CancellationToken.None);
            UIPackageMetadata packageMetadata = result.FirstOrDefault(
                p => p.Identity.Version == new NuGetVersion("4.0.0-beta1"));

            Assert.True(packageMetadata.DependencySets.Count() == 1);
            Assert.True(packageMetadata.DependencySets.First().Dependencies.Count().Equals(12));

            IEnumerable<UIPackageMetadata> packageMetadataList = resource.GetMetadata("Nuget.core", true, true, CancellationToken.None).Result;
            Assert.True(packageMetadataList != null);
            Assert.True(packageMetadataList.Count() == 46);
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestAllSearchTitle(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            UISearchResource resource = repo.GetResource<UISearchResource>();

            SearchFilter filter = new SearchFilter();
            string searchTerm = "Json";

            IEnumerable<UISearchMetadata> uiSearchResults = await resource.Search(searchTerm, filter, 0, 100, new CancellationToken());

            UISearchMetadata metadata = uiSearchResults.Where(e => StringComparer.OrdinalIgnoreCase.Equals("newtonsoft.json", e.Identity.Id)).Single();

            // TODO: check the title value once the server is updated
            Assert.True(!String.IsNullOrEmpty(metadata.Title));
            Assert.True(!String.IsNullOrEmpty(metadata.LatestPackageMetadata.Title));
        }


        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestAllSearchResources(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            UISearchResource resource = repo.GetResource<UISearchResource>();
            SearchLatestResource latestResource = repo.GetResource<SearchLatestResource>();

            //Check if we are able to obtain a resource
            Assert.True(resource != null);
            //check if the resource is of type IVsSearch.
            SearchFilter filter = new SearchFilter(); //create a dummy filter.
            List<FrameworkName> fxNames = new List<FrameworkName>();
            fxNames.Add(new FrameworkName(".NET Framework, Version=4.0"));
            filter.SupportedFrameworks = fxNames.Select(e => e.ToString());
            string SearchTerm = "Elmah";
            
            IEnumerable<UISearchMetadata> uiSearchResults = await resource.Search(SearchTerm, filter, 0, 100, new CancellationToken());
            var latestSearchResults = await latestResource.Search(SearchTerm, filter, 0, 100, CancellationToken.None);

            // Check if non empty search result is returned.
            Assert.True(uiSearchResults.Count() > 0);

            //check if there is atleast one result which has Id exactly as the search terms.
            Assert.True(uiSearchResults.Any(p => p.Identity.Id.Equals(SearchTerm, StringComparison.OrdinalIgnoreCase)));

            foreach (var result in uiSearchResults)
            {
                Assert.Equal(result.Identity.Id, result.LatestPackageMetadata.Identity.Id);
                Assert.Equal(result.Identity.Version.ToNormalizedString(), result.LatestPackageMetadata.Identity.Version.ToNormalizedString());
            }

            // Verify search and latest search return the same results
            var searchEnumerator = uiSearchResults.GetEnumerator();
            var latestEnumerator = latestSearchResults.GetEnumerator();

            for (int i=0; i < 10; i++)
            {
                searchEnumerator.MoveNext();
                latestEnumerator.MoveNext();

                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.Identity.Id, latestEnumerator.Current.Identity.Id);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.LicenseUrl, latestEnumerator.Current.LicenseUrl);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.ReportAbuseUrl, latestEnumerator.Current.ReportAbuseUrl);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.RequireLicenseAcceptance, latestEnumerator.Current.RequireLicenseAcceptance);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.Summary, latestEnumerator.Current.Summary);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.Authors, latestEnumerator.Current.Authors);
                Assert.Equal(searchEnumerator.Current.LatestPackageMetadata.Title, latestEnumerator.Current.Title);
            }

            PSSearchResource psResource = repo.GetResource<PSSearchResource>();
            IEnumerable<PSSearchMetadata> psSearchResults =  await psResource.Search(SearchTerm, filter, 0, 100, new System.Threading.CancellationToken());
            SimpleSearchResource simpleSearch = repo.GetResource<SimpleSearchResource>();
            IEnumerable<SimpleSearchMetadata> simpleSearchResults = await simpleSearch.Search(SearchTerm, filter, 0, 100, new System.Threading.CancellationToken());
            //Check that exact search results are returned irrespective of search resource ( UI, powershell and commandline).
            Assert.True(uiSearchResults.Count() == psSearchResults.Count());
            Assert.True(psSearchResults.Count() == simpleSearchResults.Count());
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestPowerShellAutocompleteResourceForPackageIds(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            PSAutoCompleteResource resource = repo.GetResource<PSAutoCompleteResource>();
            //Check if we are able to obtain a resource
            Assert.True(resource != null);
            IEnumerable<string> searchResults = await resource.IdStartsWith("Elmah", true, CancellationToken.None);
            // Check if non empty search result is returned.
            Assert.True(searchResults.Count() > 0);
            //Make sure that the results starts with the given prefix.
            Assert.True(searchResults.All(p => p.StartsWith("Elmah", StringComparison.OrdinalIgnoreCase)));
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestPowerShellAutocompleteResourceForPackageVersions(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            PSAutoCompleteResource resource = repo.GetResource<PSAutoCompleteResource>();
            //Check if we are able to obtain a resource
            Assert.True(resource != null);
            // Check if non zero version count is returned. *TODOS : Use a standard test packages whose version count will be fixed
            IEnumerable<NuGetVersion> versions = await resource.VersionStartsWith("Nuget.core", "1", true, CancellationToken.None);
            Assert.True(versions.Count() == 13);
        }

        [Theory]
        [InlineData("https://nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task TestDependencyInfoResourceForPackageWithAnyFramework(string SourceUrl)
        {
            SourceRepository repo = GetSourceRepository(SourceUrl);
            DepedencyInfoResource resource = repo.GetResource<DepedencyInfoResource>();
            //Check if we are able to obtain a resource
            Assert.True(resource != null);          
            List<PackageIdentity> packageIdentities = new List<PackageIdentity>();
            //Check the dependency tree depth for a known package. Since the same test executes for both V2 and V3 source, we cna also ensure that the pre-resolver data is same for both V2 and V3.
            packageIdentities.Add(new PackageIdentity("WebGrease",new NuGetVersion("1.6.0")));

            Stopwatch timer = new Stopwatch();
            timer.Start();

            IEnumerable<PackageDependencyInfo> packages = await resource.ResolvePackages(packageIdentities, NuGet.Frameworks.NuGetFramework.AnyFramework, true, new CancellationToken());

            timer.Stop();

            Assert.True(packages.Count() >= 16);
        }

       [Fact]
        public async Task TestAllBasicScenariosForLocalShare()
        {
           
            List<PackageIdentity> packages = new List<PackageIdentity>();        
            packages.Add(new PackageIdentity("Nuget.core", new NuGetVersion("2.8.3")));
            packages.Add(new PackageIdentity("Nuget.core", new NuGetVersion("2.5.0")));

            //create a local package source by downloading the specific packages from remote feed.
            SetupLocalShare(packages); 
           
           //Create source repo based on the local share.
            SourceRepository repo = GetSourceRepository(Environment.CurrentDirectory);
            UIMetadataResource resource = repo.GetResource<UIMetadataResource>();
            Assert.True(resource != null);
           
           //check if UIPackageMetadata works fine.
            IEnumerable<UIPackageMetadata> packageMetadataList =  resource.GetMetadata("Nuget.core", true, true, CancellationToken.None).Result;
            Assert.True(packageMetadataList != null);
            Assert.True(packageMetadataList.Count() == 2);
            Assert.True(packageMetadataList.All(item => item.Tags.Contains("nuget")));
            Assert.True(packageMetadataList.All(item => item.RequireLicenseAcceptance.Equals(false)));
            Assert.True(packageMetadataList.All(item => item.ProjectUrl.ToString().Equals("http://nuget.codeplex.com/")));           
            Assert.True(packageMetadataList.Any(item => item.DependencySets.Count() == 1)); 
            Assert.True(packageMetadataList.First(item => item.DependencySets.Count()==1).DependencySets.First().Dependencies.Any(item2 => item2.Id.Equals("Microsoft.Web.Xdt", StringComparison.OrdinalIgnoreCase)));

            //Check if downloadresource works fine.
            DownloadResource downloadResource = repo.GetResource<DownloadResource>();
            Uri downloadUrl =  await downloadResource.GetDownloadUrl(new PackageIdentity("Nuget.core", new NuGetVersion("2.5.0")));
            Assert.True(downloadUrl.IsFile);
            Assert.True(File.Exists(downloadUrl.LocalPath)); //path doesnt contain the folder name and also the version is normalized in path for local scenario.

           //Check if metadata resource works fine.
            MetadataResource metadataResource = repo.GetResource<MetadataResource>();
            NuGetVersion latestVersion = await  metadataResource.GetLatestVersion("Nuget.core", true, false, CancellationToken.None);
            Assert.True(latestVersion.ToNormalizedString().Contains("2.8.3"));
        }

       [Theory]
       [InlineData("https://nuget.org/api/v2/")]
       [InlineData("https://api.nuget.org/v3/index.json")]     
       public async Task TestAllResourcesForNonExistentPackage(string SourceUrl)
       {
           string packageId = "nonexistentpackage";
           string version = "1.0";
           SourceRepository repo = GetSourceRepository(SourceUrl);

           DownloadResource downloadResource = repo.GetResource<DownloadResource>();
           Assert.True(downloadResource != null);
           Uri downloadMetadata = await downloadResource.GetDownloadUrl(new PackageIdentity(packageId, new NuGetVersion(version)));
           Assert.True(downloadMetadata == null);
           
           MetadataResource metadataResource = repo.GetResource<MetadataResource>();
           Assert.True(metadataResource != null);
           NuGetVersion latestVersion = await metadataResource.GetLatestVersion(packageId, true, true, CancellationToken.None);
           Assert.True(latestVersion == null);

           UIMetadataResource uiMetadataResource = repo.GetResource<UIMetadataResource>();
           Assert.True(uiMetadataResource != null);

           var result = await uiMetadataResource.GetMetadata(packageId, true, true, CancellationToken.None);
           Assert.False(result.Any());

           DepedencyInfoResource resource = repo.GetResource<DepedencyInfoResource>();
           //Check if we are able to obtain a resource
           Assert.True(resource != null);
           List<PackageIdentity> packageIdentities = new List<PackageIdentity>();          
           packageIdentities.Add(new PackageIdentity(packageId, new NuGetVersion(version)));
           IEnumerable<PackageDependencyInfo> packages = await resource.ResolvePackages(packageIdentities, NuGet.Frameworks.NuGetFramework.AnyFramework, true, new CancellationToken());
           Assert.True(packages == null || packages.Count() == 0);
       }

        #region PrivateHelpers
        private SourceRepository GetSourceRepository(string SourceUrl)
        {
            try
            {
                IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();

                StringBuilder sb = new StringBuilder();

                foreach (var provider in providers)
                {
                    sb.AppendLine(provider.Metadata.ResourceType.ToString());
                }

                Assert.True(providers.Count() > 0);
                NuGet.Configuration.PackageSource source = new NuGet.Configuration.PackageSource(SourceUrl, "mysource", true);
                SourceRepository repo = new SourceRepository(source, providers);
                return repo;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private void SetupLocalShare(IEnumerable<PackageIdentity> packages)
        {
            NuGet.MachineCache.Default.Clear();
            foreach (var identity in packages)
            {
                string id = identity.Id;
                NuGet.SemanticVersion version = new NuGet.SemanticVersion(identity.Version.ToNormalizedString());
                new PackageManager(PackageRepositoryFactory.Default.CreateRepository(V2SourceUrl), Environment.CurrentDirectory).InstallPackage(id, version);
            }         
        }

        #endregion PrivateHelpers


    }
}
