using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    /// <summary>
    /// Resource wrapper for an HttpClient
    /// </summary>
    public class V3HttpHandlerResource : HttpHandlerResource
    {
        private readonly HttpMessageHandler _messageHandler;

        public V3HttpHandlerResource(HttpMessageHandler messageHandler)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException("messageHandler");
            }

            _messageHandler = messageHandler;
        }

        public override HttpMessageHandler MessageHandler
        {
            get { return _messageHandler; }
        }
    }
}
