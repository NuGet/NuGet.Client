using NuGet.Client;
using NuGet.Client.V3.VisualStudio;
using NuGet.Client.VisualStudio;
using NuGet.Configuration;
//using NuGet.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace V3WebTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //PackageSource source = new PackageSource("https://az320820.vo.msecnd.net/ver3-preview/index.json", "RC V3");

            //List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = new List<Lazy<INuGetResourceProvider,INuGetResourceProviderMetadata>>();

            //DataClient client = new DataClient();

            //INuGetResourceProviderMetadata attribute = new ResourceMetadata(typeof(UISearchResource)) as INuGetResourceProviderMetadata;

            //providers.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new V3UISearchResourceProvider(client), attribute));

            //providers.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new V3ServiceIndexResourceProvider(client), new ResourceMetadata(typeof(V3ServiceIndexResource))));

            //SourceRepository repo = new SourceRepository(source, providers);

            //var resource = repo.GetResource<UISearchResource>();

            //var task = resource.Search("nuget", new SearchFilter(), 0, 30, CancellationToken.None);
            //task.Wait();

            //var results = task.Result;
        }
    }

    public class ResourceMetadata : INuGetResourceProviderMetadata
    {
        private Type _type;

        public ResourceMetadata(Type type)
        {
            _type = type;
        }

        public Type ResourceType
        {
            get { return _type; }
        }

        public IEnumerable<string> After
        {
            get { return new string[0]; }
        }

        public IEnumerable<string> Before
        {
            get { return new string[0]; }
        }

        public string Name
        {
            get { return string.Empty; }
        }
    }
}
