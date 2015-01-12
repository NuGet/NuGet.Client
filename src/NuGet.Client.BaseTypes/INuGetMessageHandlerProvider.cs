using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Extension point for providing Http Message handlers to do proxy and authentication
    /// </summary>
    public interface INuGetMessageHandlerProvider
    {
        bool TryCreate(PackageSource source, out DelegatingHandler handler);
    }
}
