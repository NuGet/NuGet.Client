using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Test.Utility
{
    public static class StaticHttpHandler
    {
        /// <summary>
        /// Creates a handler to override url requests to static content
        /// </summary>
        public static TestHttpHandlerProvider CreateHttpHandler(Dictionary<string, string> responses)
        {
            return new TestHttpHandlerProvider(new TestMessageHandler(responses));
        }

        /// <summary>
        /// Creates a source and injects an http handler to override the normal http calls
        /// </summary>
        public static SourceRepository CreateSource(string sourceUrl, IEnumerable<Lazy<INuGetResourceProvider>> providers, Dictionary<string, string> responses)
        {
            var handler = new Lazy<INuGetResourceProvider>(() => CreateHttpHandler(responses));

            return new SourceRepository(new PackageSource(sourceUrl), providers.Concat(new Lazy<INuGetResourceProvider>[] { handler }));
        }
    }

    public class TestHttpHandlerProvider : ResourceProvider
    {
        private HttpMessageHandler _messageHandler;

        public TestHttpHandlerProvider(HttpMessageHandler messageHandler)
            : base(typeof(HttpHandlerResource), "testhandler", NuGetResourceProviderPositions.First)
        {
            _messageHandler = messageHandler;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            return new Tuple<bool, INuGetResource>(true, new TestHttpHandler(_messageHandler));
        }
    }

    public class TestHttpHandler : HttpHandlerResource
    {
        private HttpMessageHandler _messageHandler;

        public TestHttpHandler(HttpMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }

        public override HttpMessageHandler MessageHandler
        {
            get
            {
                return _messageHandler;
            }
        }
    }

    public class TestMessageHandler : HttpMessageHandler
    {
        private Dictionary<string, string> _responses;

        public TestMessageHandler(Dictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage msg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            string s = null;
            if (_responses.TryGetValue(request.RequestUri.AbsoluteUri, out s))
            {
                // TODO: allow s to be a status code to return

                if (String.IsNullOrEmpty(s))
                {
                    msg = new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                else
                {
                    msg.Content = new TestContent(s);
                }
            }
            else
            {
                throw new Exception("Unhandled test request: " + request.RequestUri.AbsoluteUri);
            }

            return msg;
        }
    }

    public class TestContent : HttpContent
    {
        private MemoryStream _stream;

        public TestContent(string s)
        {
            _stream = new MemoryStream(Encoding.UTF8.GetBytes(s));
            _stream.Seek(0, SeekOrigin.Begin);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            _stream.CopyTo(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = (long)_stream.Length;
            return true;
        }
    }
}
