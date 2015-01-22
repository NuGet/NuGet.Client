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
            resource = null;

            var repositoryResource = source.GetResource<V2PackageRepositoryResource>();

            if (repositoryResource != null && repositoryResource.V2Client != null)
            {
                resource = repositoryResource;
            }

            return resource != null;
        }
    }
}