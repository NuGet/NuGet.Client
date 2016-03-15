using System.IO;

namespace NuGet.Protocol
{
    public class HttpCacheResult
    {
        public string CacheFileName { get; set; }
        public Stream Stream { get; set; }
    }
}