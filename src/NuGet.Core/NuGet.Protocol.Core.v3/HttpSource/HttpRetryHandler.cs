// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Logging;
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
            ILogger log,
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
                    var stopwatch = Stopwatch.StartNew();
                    string requestUri = request.RequestUri.ToString();
                    
                    try
                    {
                        var timeoutMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Http_Timeout,
                            request.Method,
                            requestUri,
                            (int)RequestTimeout.TotalMilliseconds);

                        log.LogInformation("  " + string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.Http_RequestLog,
                            request.Method,
                            requestUri));

                        response = await TimeoutUtility.StartWithTimeout(
                            timeoutToken => client.SendAsync(request, completionOption, timeoutToken),
                            RequestTimeout,
                            timeoutMessage,
                            cancellationToken);

                        log.LogInformation("  " + string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.Http_ResponseLog,
                            response.StatusCode,
                            requestUri,
                            stopwatch.ElapsedMilliseconds));

                        if ((int)response.StatusCode >= 500)
                        {
                            success = false;
                        }
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        success = false;

                        if (tries >= MaxTries)
                        {
                            throw;
                        }

                        log.LogInformation(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_RetryingHttp,
                            request.Method,
                            requestUri,
                            request)
                            + Environment.NewLine
                            + ExceptionUtilities.DisplayMessage(e));
                    }
                }
            }

            return response;
        }
    }
}
