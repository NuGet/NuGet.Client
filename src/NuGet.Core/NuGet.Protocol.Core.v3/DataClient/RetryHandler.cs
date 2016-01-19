// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    public class RetryHandler : DelegatingHandler
    {
        /// <summary>
        /// Total number of requests allowed for a single call.
        /// </summary>
        public int MaxTries { get; }
        public TimeSpan RetryDelay { get; }

        public RetryHandler(HttpMessageHandler innerHandler, int maxTries)
            : base(innerHandler)
        {
            MaxTries = maxTries;
            RetryDelay = TimeSpan.FromMilliseconds(200);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var tries = 0;

            HttpResponseMessage response = null;

            var success = false;

            while (tries < MaxTries
                   && !success)
            {
                // wait before trying again if the last call failed
                if (!success
                    && tries > 0)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }

                tries++;
                success = true;

                try
                {
                    response = await base.SendAsync(request, cancellationToken);

                    var statusCode = (int)response.StatusCode;

                    // fail on 5xx responses
                    if (statusCode >= 500)
                    {
                        success = false;
                    }
                }
                catch
                {
                    success = false;

                    // suppress exceptions until the final try
                    if (tries >= MaxTries)
                    {
                        throw;
                    }
                }
            }

            return response;
        }
    }
}
