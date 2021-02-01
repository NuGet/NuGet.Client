// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Events;

namespace NuGet.Protocol
{
    /// <summary>
    /// The <see cref="HttpRetryHandler"/> is for retrying and HTTP request if it times out, has any exception,
    /// or returns a status code of 500 or greater.
    /// </summary>
    public class HttpRetryHandler : IHttpRetryHandler
    {
        internal const string StopwatchPropertyName = "NuGet_ProtocolDiagnostics_Stopwatches";

        /// <summary>
        /// Make an HTTP request while retrying after failed attempts or timeouts.
        /// </summary>
        /// <remarks>
        /// This method accepts a factory to create instances of the <see cref="HttpRequestMessage"/> because
        /// requests cannot always be used. For example, suppose the request is a POST and contains content
        /// of a stream that can only be consumed once.
        /// </remarks>
        public Task<HttpResponseMessage> SendAsync(
            HttpRetryHandlerRequest request,
            ILogger log,
            CancellationToken cancellationToken)
        {
            return SendAsync(request, source: string.Empty, log, cancellationToken);
        }

        /// <summary>
        /// Make an HTTP request while retrying after failed attempts or timeouts.
        /// </summary>
        /// <remarks>
        /// This method accepts a factory to create instances of the <see cref="HttpRequestMessage"/> because
        /// requests cannot always be used. For example, suppose the request is a POST and contains content
        /// of a stream that can only be consumed once.
        /// </remarks>
        public async Task<HttpResponseMessage> SendAsync(
            HttpRetryHandlerRequest request,
            string source,
            ILogger log,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var tries = 0;
            HttpResponseMessage response = null;
            var success = false;

            while (tries < request.MaxTries && !success)
            {
                if (tries > 0)
                {
                    await Task.Delay(request.RetryDelay, cancellationToken);
                }

                tries++;
                success = true;

                using (var requestMessage = request.RequestFactory())
                {
                    var stopwatches = new List<Stopwatch>(2);
                    var bodyStopwatch = new Stopwatch();
                    stopwatches.Add(bodyStopwatch);
                    Stopwatch headerStopwatch = null;
                    if (request.CompletionOption == HttpCompletionOption.ResponseHeadersRead)
                    {
                        headerStopwatch = new Stopwatch();
                        stopwatches.Add(headerStopwatch);
                    }
#if NET5_0
                    requestMessage.Options.Set(new HttpRequestOptionsKey<List<Stopwatch>>(StopwatchPropertyName), stopwatches);
#else
                    requestMessage.Properties[StopwatchPropertyName] = stopwatches;
#endif
                    var requestUri = requestMessage.RequestUri;

                    try
                    {
                        // The only time that we will be disposing this existing response is if we have 
                        // successfully fetched an HTTP response but the response has an status code indicating
                        // failure (i.e. HTTP status code >= 500).
                        // 
                        // If we don't even get an HTTP response message because an exception is thrown, then there
                        // is no response instance to dispose. Additionally, we cannot use a finally here because
                        // the caller needs the response instance returned in a non-disposed state.
                        //
                        // Also, remember that if an HTTP server continuously returns a failure status code (like
                        // 500 Internal Server Error), we will retry some number of times but eventually return the
                        // response as-is, expecting the caller to check the status code as well. This results in the
                        // success variable being set to false but the response being returned to the caller without
                        // disposing it.
                        response?.Dispose();

                        // Add common headers to the request after it is created by the factory. This includes
                        // X-NuGet-Session-Id which is added to all nuget requests.
                        foreach (var header in request.AddHeaders)
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }

                        log.LogInformation("  " + string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.Http_RequestLog,
                            requestMessage.Method,
                            requestUri));

                        // Issue the request.
                        var timeoutMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Http_Timeout,
                            requestMessage.Method,
                            requestUri,
                            (int)request.RequestTimeout.TotalMilliseconds);
                        response = await TimeoutUtility.StartWithTimeout(
                            async timeoutToken =>
                            {
                                bodyStopwatch.Start();
                                headerStopwatch?.Start();
                                var responseMessage = await request.HttpClient.SendAsync(requestMessage, request.CompletionOption, timeoutToken);
                                headerStopwatch?.Stop();
                                return responseMessage;
                            },
                            request.RequestTimeout,
                            timeoutMessage,
                            cancellationToken);

                        // Wrap the response stream so that the download can timeout.
                        if (response.Content != null)
                        {
                            var networkStream = await response.Content.ReadAsStreamAsync();
                            var timeoutStream = new DownloadTimeoutStream(requestUri.ToString(), networkStream, request.DownloadTimeout);
                            var inProgressEvent = new ProtocolDiagnosticInProgressHttpEvent(
                                source,
                                requestUri,
                                headerStopwatch?.Elapsed,
                                (int)response.StatusCode,
                                isRetry: request.IsRetry || tries > 1,
                                isCancelled: false,
                                isLastAttempt: tries == request.MaxTries && request.IsLastAttempt);
                            var diagnosticsStream = new ProtocolDiagnosticsStream(timeoutStream, inProgressEvent, bodyStopwatch, ProtocolDiagnostics.RaiseEvent);

                            var newContent = new StreamContent(diagnosticsStream);

                            // Copy over the content headers since we are replacing the HttpContent instance associated
                            // with the response message.
                            foreach (var header in response.Content.Headers)
                            {
                                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }

                            response.Content = newContent;
                        }

                        log.LogInformation("  " + string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.Http_ResponseLog,
                            response.StatusCode,
                            requestUri,
                            bodyStopwatch.ElapsedMilliseconds));

                        if ((int)response.StatusCode >= 500)
                        {
                            success = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        response?.Dispose();

                        ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpEvent(
                            timestamp: DateTime.UtcNow,
                            source,
                            requestUri,
                            headerDuration: null,
                            eventDuration: bodyStopwatch.Elapsed,
                            httpStatusCode: null,
                            bytes: 0,
                            isSuccess: false,
                            isRetry: request.IsRetry || tries > 1,
                            isCancelled: true,
                            isLastAttempt: tries == request.MaxTries && request.IsLastAttempt));

                        throw;
                    }
                    catch (Exception e)
                    {
                        success = false;

                        response?.Dispose();

                        ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpEvent(
                            timestamp: DateTime.UtcNow,
                            source,
                            requestUri,
                            headerDuration: null,
                            eventDuration: bodyStopwatch.Elapsed,
                            httpStatusCode: null,
                            bytes: 0,
                            isSuccess: false,
                            isRetry: request.IsRetry || tries > 1,
                            isCancelled: false,
                            isLastAttempt: tries == request.MaxTries && request.IsLastAttempt));

                        if (tries >= request.MaxTries)
                        {
                            throw;
                        }

                        log.LogInformation(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_RetryingHttp,
                            requestMessage.Method,
                            requestUri,
                            requestMessage)
                            + Environment.NewLine
                            + ExceptionUtilities.DisplayMessage(e));
                    }
                }
            }

            return response;
        }
    }
}
