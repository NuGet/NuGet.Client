using System;
using System.IO;

namespace NuGet.Protocol
{
    public class HttpSourceResult : IDisposable
    {
        public Stream Stream { get; private set; }
        public HttpSourceResultStatus Status { get; }
        public string CacheFileName { get; }

        public HttpSourceResult(HttpSourceResultStatus status, string cacheFileName, Stream stream)
        {
            Stream = stream;
            Status = status;
            CacheFileName = cacheFileName;
        }

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