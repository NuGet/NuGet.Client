// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class RetryLoop
    {
        private readonly int _maxTries;
        private readonly TimeSpan _requestTimeout;
        private readonly TimeSpan _retryDelay;

        /// <summary>
        /// The <see cref="RetryLoop"/> is for retrying and HTTP request if it times out, has any exception,
        /// or returns a status code of 500 or greater.
        /// </summary>
        /// <param name="maxTries">
        /// The maximum number of times to try the request. This value includes the initial attempt.
        /// </param>
        /// <param name="requestTimeout">How long to wait on the request to come back with a response.</param>
        /// <param name="retryDelay">How long to wait before trying again after a failed request.</param>
        public RetryLoop(int maxTries, TimeSpan requestTimeout, TimeSpan retryDelay)
        {
            _maxTries = maxTries;
            _requestTimeout = requestTimeout;
            _retryDelay = retryDelay;
        }

        public async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            HttpRequestMessage request,
            Func<HttpRequestMessage, HttpRequestMessage> factoryToRecreateRequestOnRetry,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            var tries = 0;
            HttpResponseMessage response = null;
            var success = false;

            while (tries < _maxTries && !success)
            {
                if (tries > 0)
                {
                    if (factoryToRecreateRequestOnRetry != null)
                    {
                        request = factoryToRecreateRequestOnRetry(request);
                    }
                    else
                    {
                        request = request.Clone();
                    }
                    await Task.Delay(_retryDelay, cancellationToken);
                }

                tries++;
                success = true;

                try
                {
                    /*
                     * Implement timeout. Two operations are started and run in parallel:
                     *
                     *   1) The HTTP request sent in by the caller is sent to HttpClient.
                     *   2) A timer that fires after the duration of the timeout.
                     *
                     * If the timeout occurs first, the HTTP request should be cancelled. If the 
                     * HTTP request completes before the timeout, the timeout should be cancelled.
                     * If the timeout occurs first, consider this request a failure and, if all
                     * retries are exhausted, throw a timeout exception to be clear to the user
                     * what happened. If the request completes first, it could be that the response
                     * came back or that the caller cancelled the request.
                     */
                    using (var timeoutTcs = new CancellationTokenSource())
                    using (var responseTcs = new CancellationTokenSource())
                    using (cancellationToken.Register(() => responseTcs.Cancel()))
                    {
                        var timeoutTask = Task.Delay(_requestTimeout, timeoutTcs.Token);
                        var responseTask = client.SendAsync(request, completionOption, responseTcs.Token);

                        if (timeoutTask == await Task.WhenAny(responseTask, timeoutTask))
                        {
                            responseTcs.Cancel();
                            success = false;

                            if (tries >= _maxTries)
                            {
                                var message = string.Format(CultureInfo.CurrentCulture, Strings.Http_Timeout, request.Method, request.RequestUri);
                                throw new TimeoutException(message);
                            }
                        }
                        else
                        {
                            timeoutTcs.Cancel();
                            cancellationToken.ThrowIfCancellationRequested();
                            response = responseTask.Result;

                            if ((int)response.StatusCode >= 500)
                            {
                                success = false;
                            }
                        }
                    }
                }
                catch
                {
                    success = false;

                    if (tries >= _maxTries)
                    {
                        throw;
                    }
                }
            }

            return response;
        }
    }
}
