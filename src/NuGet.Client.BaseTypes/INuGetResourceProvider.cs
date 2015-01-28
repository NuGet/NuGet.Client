using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// INuGetResourceProviders are imported by SourceRepository. They exist as singletons which span all sources, and are responsible 
    /// for determining if they should be used for the given source when TryCreate is called. 
    /// 
    /// The provider determines the caching. Resources may be cached per source, but they are normally created new each time 
    /// to allow for caching within the context they were created in.
    /// 
    /// Providers may retrieve other resources from the source repository and pass them to the resources they create in order
    /// to build on them.
    /// </summary>
    public interface INuGetResourceProvider
    {
        /// <summary>
        /// Attempts to create a resource for this source.
        /// </summary>
        /// <remarks>The provider may return true but null for the resource if the 
        /// provider determines that it should not exist.</remarks>
        /// <param name="source">Source repository</param>
        /// <returns>True if this provider handles the input source.</returns>
        Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token);
    }
}
