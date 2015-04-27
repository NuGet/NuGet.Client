using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Resource wrapper for an HttpClient
    /// </summary>
    public class HttpHandlerResourceV3 : HttpHandlerResource
    {
        private readonly HttpMessageHandler _messageHandler;

        public HttpHandlerResourceV3(HttpMessageHandler messageHandler)
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
