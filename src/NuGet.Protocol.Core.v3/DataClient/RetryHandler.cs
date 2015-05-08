// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
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
            var tries = 0;

            HttpResponseMessage response = null;

            var success = false;

            while (tries < _retries
                   && !success)
            {
                // wait progressively longer
                if (!success
                    && tries > 0)
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
