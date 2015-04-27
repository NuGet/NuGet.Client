using System.Net.Http;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// An HttpClient configured for the package source
    /// </summary>
    public abstract class HttpHandlerResource : INuGetResource
    {
        /// <summary>
        /// HttpClient resource
        /// </summary>
        public abstract HttpMessageHandler MessageHandler { get; }
    }
}
