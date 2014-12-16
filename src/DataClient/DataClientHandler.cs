using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class DataClientHandler : DelegatingHandler
    {
        private readonly FileCacheBase _fileCache;
        private readonly EntityCache _entityCache;

        public DataClientHandler(HttpMessageHandler innerHandler, HttpClient client)
            : base(innerHandler)
        {

        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TODO: import all credential providers and give them a chance to modify this request first

            throw new Exception("bkah");

            return base.SendAsync(request, cancellationToken);
        }
    }
}
