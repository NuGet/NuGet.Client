using System;
using System.IO;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    internal class HttpSourceResult : IDisposable
    {
        public string CacheFileName { get; set; }
        public Stream Stream { get; set; }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.Dispose();
                Stream = null;
            }
        }
    }
}
