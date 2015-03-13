#if !DNXCORE50
using System;
using System.Net;

namespace NuGet.Configuration
{
    public interface IProxyCache
    {
        void Add(IWebProxy proxy);
        IWebProxy GetProxy(Uri uri);
    }
}
#endif