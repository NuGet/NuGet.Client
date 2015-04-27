using NuGet.Configuration;
using System.Net.Http;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Extension point for providing Http Message handlers to do proxy and authentication
    /// </summary>
    public interface INuGetMessageHandlerProvider
    {
        bool TryCreate(PackageSource source, out DelegatingHandler handler);
    }
}
