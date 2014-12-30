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

namespace TestAppV2V3Switching
{
    class Program
    {
        private CompositionContainer container;
        public void AssembleComponents()
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

        public void TestV3Download()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);    
            PackageSource source = new PackageSource("V3Source", "https://az320820.vo.msecnd.net/ver3-preview/index.json");
            SourceRepository2 repo = new SourceRepository2(source,providers);
            IDownload resource = (IDownload)repo.GetResource<IDownload>();
            Debug.Assert(resource != null);
            Debug.Assert(resource.GetType().GetInterfaces().Contains(typeof(IDownload)));
            PackageDownloadMetadata downloadMetadata = resource.GetNupkgUrlForDownload(new PackageIdentity("jQuery", new NuGetVersion("1.6.4"))).Result;
            Debug.Assert(downloadMetadata.NupkgDownloadUrl.OriginalString.EndsWith(".nupkg")); //*TODOs: Check if the download Url ends with .nupkg. More detailed verification can be added to see if the nupkg file can be fetched from the location.
        }

        public void TestV3Metadata()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);         
            PackageSource source = new PackageSource("V3Source", @"C:\temp\my.json");
            SourceRepository2 repo = new SourceRepository2(source,providers);
            IMetadata resource = (IMetadata)repo.GetResource<IMetadata>();
            Debug.Assert(resource != null);
            Debug.Assert(resource.GetType().GetInterfaces().Contains(typeof(IMetadata)));
            NuGetVersion latestVersion = resource.GetLatestVersion("jQuery").Result;
            Debug.Assert(latestVersion.ToNormalizedString().Equals("2.1.1")); //*TODOs: Use a proper test package whose latest version is fixed instead of using jQuery.
        }

        public async void TestV3Search()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);      
            PackageSource source = new PackageSource("V3Source", "https://az320820.vo.msecnd.net/ver3-preview/index.json");
            SourceRepository2 repo = new SourceRepository2(source,providers);
            IVsSearch resource = (IVsSearch)repo.GetResource<IVsSearch>();             
            Debug.Assert(resource != null); //Check if we are able to obtain a resource
            Debug.Assert(resource.GetType().GetInterfaces().Contains(typeof(IVsSearch))); //check if the resource is of type IVsSearch.
            SearchFilter filter = new SearchFilter(); //create a dummy filter.
            List<FrameworkName> fxNames = new List<FrameworkName>();
            fxNames.Add(new FrameworkName(".NET Framework, Version=4.0"));
            filter.SupportedFrameworks = fxNames;
            IEnumerable<VisualStudioUISearchMetadata> searchResults = resource.GetSearchResultsForVisualStudioUI("Elmah", filter, 0, 100, new System.Threading.CancellationToken()).Result;
            Debug.Assert(searchResults.Count() > 0); // Check if non empty search result is returned.
            Debug.Assert(searchResults.Any(p => p.Id.Equals("Elmah", StringComparison.OrdinalIgnoreCase))); //check if there is atleast one result which has Elmah as title.
        }

        static void Main(string[] args)
        {

            Uri uri = new Uri(@"C:\temp\index.json");
            Console.WriteLine(uri.IsFile);
            Console.WriteLine(uri.IsUnc);
            Console.WriteLine(uri.LocalPath);
            
            Program p = new Program();
            p.AssembleComponents();
            p.TestV3Download();
            p.TestV3Metadata();
            p.TestV3Search();

            

        }
    }
}
