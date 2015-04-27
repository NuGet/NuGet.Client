using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly int _retries;

        public RetryHandler(HttpMessageHandler innerHandler, int retries)
            : base(innerHandler)
        {
            _retries = retries;
        }

        // TODO: Revist this logic
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int tries = 0;

            HttpResponseMessage response = null;

            bool success = false;

            while (tries < _retries && !success)
            {
                // wait progressively longer
                if (!success && tries > 0)
                {
                    await Task.Delay(500 * tries, cancellationToken);
                }

                tries++;

                try
                {
                    response = await base.SendAsync(request, cancellationToken);

                    success = response.IsSuccessStatusCode;
                }
                catch
                {
                    // suppress exceptions until the final try
                    if (tries >= _retries)
                    {
                        throw;
                    }
                }
            }

            return response;
        }
    }
}
