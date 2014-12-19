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

namespace TestAppV2V3Switching
{
    class Program
    {
        private CompositionContainer container;
        public  void AssembleComponents()
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
                     
        public void TestGetResourceGivesRequiredResourceType()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);
            PackageSource source = new PackageSource("nuget.org", "https://nuget.org/api/v2");
            SourceRepository2 repo = new SourceRepository2(source, providers);
            IDownload resource = (IDownload)repo.GetResource<IDownload>();
            Debug.Assert(resource != null);
            Debug.Assert(resource.GetType().GetInterfaces().Contains(typeof(IDownload)));
        }

        public void TestAppropriateExceptionThrownWhenResourceIsNotAvailable()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);
            PackageSource source = new PackageSource("nuget.org", "https://nuget.org/api/v2");
            SourceRepository2 repo = new SourceRepository2(source, providers);
            IMetrics resource = (IMetrics)repo.GetResource<IMetrics>();
            Debug.Assert(resource == null); // no metrics resource would be availabe for v2 source.            
        }

        public void TestE2E()
        {
            IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers = container.GetExports<ResourceProvider, IResourceProviderMetadata>();
            Debug.Assert(providers.Count() > 0);
            PackageSource source = new PackageSource("nuget.org", "https://nuget.org/api/v2");
            SourceRepository2 repo = new SourceRepository2(source, providers);
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
            Program p = new Program();
            p.AssembleComponents();
            p.TestGetResourceGivesRequiredResourceType();
            p.TestAppropriateExceptionThrownWhenResourceIsNotAvailable();
            p.TestE2E();
            
        }
    }
}
