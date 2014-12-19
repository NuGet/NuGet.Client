using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    public abstract class V3ResourceProvider : ResourceProvider
    {        
        public override bool TryCreateResource(PackageSource source,out Resource resource)
        {
            try
            {
                object repo = null;
                string host = "TestHost";
                if (!packageSourceCache.TryGetValue(source.Url, out repo)) //Check if the source is already present in the cache.
                {                    
                    if (V3Utilities.IsV3(source)) //if it's not in cache, then check if it is V3.
                    {
                        repo = V3Utilities.GetV3Client(source, host); //Get a NuGetV3Client object and add it to the cache.
                        packageSourceCache.Add(source.Url, repo);
                    }
                    else
                    {
                        resource = null; //if it's not V3, then return.
                        return false;
                    }                   
                }
                resource = new V3Resource((NuGetV3Client)repo,host); //Create a resourc and return it.
                return true;
            }
            catch (Exception)
            {
                resource = null;
                return false; //*TODOs:Do tracing and throw apppropriate exception here.
            }
        }     
    }
}
