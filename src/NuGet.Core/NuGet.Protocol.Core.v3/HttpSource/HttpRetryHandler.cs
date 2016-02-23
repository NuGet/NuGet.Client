// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class HttpRetryHandler
    {
        /// <summary>
        /// The <see cref="HttpRetryHandler"/> is for retrying and HTTP request if it times out, has any exception,
        /// or returns a status code of 500 or greater.
        /// </summary>
        public HttpRetryHandler()
        {
            MaxTries = 3;
            RequestTimeout = TimeSpan.FromSeconds(100);
            RetryDelay = TimeSpan.FromMilliseconds(200);
        }

        /// <summary>The maximum number of times to try the request. This value includes the initial attempt.</summary>
        /// <remarks>This API is intended only for testing purposes and should not be used in product code.</remarks>
        public int MaxTries { get; set; }

        /// <summary>How long to wait on the request to come back with a response.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>How long to wait before trying again after a failed request.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public TimeSpan RetryDelay { get; set; }

        /// <summary>
        /// Make an HTTP request while retrying after failed attempts or timeouts.
        /// </summary>
        /// <remarks>
        /// This method accepts a factory to create instances of the <see cref="HttpRequestMessage"/> because
        /// requests cannot always be used. For example, suppose the request is a POST and contains content
        /// of a stream that can only be consumed once.
        /// </remarks>
        public async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            Func<HttpRequestMessage> requestFactory,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            var tries = 0;
            HttpResponseMessage response = null;
            var success = false;

            while (tries < MaxTries && !success)
            {
                if (tries > 0)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }

                tries++;
                success = true;

                using (var request = requestFactory())
                {
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
                            var timeoutTask = Task.Delay(RequestTimeout, timeoutTcs.Token);
                            var responseTask = client.SendAsync(request, completionOption, responseTcs.Token);

                            if (timeoutTask == await Task.WhenAny(responseTask, timeoutTask))
                            {
                                responseTcs.Cancel();
                                success = false;

                                if (tries >= MaxTries)
                                {
                                    var message = string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.Http_Timeout,
                                        request.Method,
                                        request.RequestUri,
                                        (int)RequestTimeout.TotalMilliseconds,
                                        Strings.Milliseconds);
                                    throw new TimeoutException(message);
                                }
                            }
                            else
                            {
                                timeoutTcs.Cancel();
                                response = await responseTask;

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

                        if (tries >= MaxTries)
                        {
                            throw;
                        }
                    }
                }
            }

            return response;
        }
    }
}
