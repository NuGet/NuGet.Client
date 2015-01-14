using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    /// <summary>
    /// Partial implementation for IResourceProvider to do the common V2 specific stuff.
    /// </summary>
    public abstract class V2ResourceProvider : INuGetResourceProvider
    {
        public virtual bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            try
            {
                object repo = null;
               
                    // if it's not in cache, then check if it is V2.
                    if (V2Utilities.IsV2(source.PackageSource).Result)
                    {
                        // Get a IPackageRepo object and add it to the cache.
                        repo = V2Utilities.GetV2SourceRepository(source.PackageSource);                       
                    }
                    else
                    {
                        // if it's not V2, returns null
                        resource = null;
                        return false;
                    }
              

                // Create a resource and return it.
                resource = new V2Resource((IPackageRepository)repo);
                return true;
            }
            catch (Exception)
            {
                // *TODOs:Do tracing and throw apppropriate exception here.
                resource = null;
                return false;
            }
        }
    }
}