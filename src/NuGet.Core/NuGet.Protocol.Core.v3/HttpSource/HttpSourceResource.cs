using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    /// <summary>
    /// Holds a shared <see cref="HttpSource"/>. 
    /// This is expected to be shared across the app and should not be disposed of.
    /// </summary>
    public class HttpSourceResource : INuGetResource
    {
        public HttpSourceResource(HttpSource httpSource)
        {
            HttpSource = httpSource;
        }

        public HttpSource HttpSource { get; }
    }
}
