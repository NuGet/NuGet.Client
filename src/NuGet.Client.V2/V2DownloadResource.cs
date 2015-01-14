using NuGet;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NuGet.PackagingCore;

namespace NuGet.Client.V2
{
    public class V2DownloadResource : DownloadResource
    {
        private readonly IPackageRepository V2Client;
        public V2DownloadResource(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public V2DownloadResource(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }
      
        public override Task<Uri> GetDownloadUrl(PackageIdentity identity, System.Threading.CancellationToken token)
        {
            //*TODOs: Temp implementation. Need to do erorr handling and stuff.
            return Task.Factory.StartNew(() =>
            {
                if (V2Client is DataServicePackageRepository)
                {
                    //TODOs:Not sure if there is some other standard way to get the Url from a dataservice repo. DataServicePackage has downloadurl property but not sure how to get it.
                    return new Uri(Path.Combine(V2Client.Source,  identity.Id + "." + identity.Version + ".nupkg"));
                }
                else if (V2Client is LocalPackageRepository)
                {
                    LocalPackageRepository lrepo = V2Client as LocalPackageRepository;
                    SemanticVersion semVer = new SemanticVersion(identity.Version.Version);
                    return new Uri(Path.Combine(V2Client.Source, lrepo.PathResolver.GetPackageFileName(identity.Id, semVer)));
                }
                else
                {
                    // TODO: move the string into a resoure file
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture,
                        "Unable to get download metadata for package {0}", identity.Id));
                }
            });
        }

        public override Task<Stream> GetStream(PackageIdentity identity, System.Threading.CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
