﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;

namespace NuGet.Protocol
{
    /// <summary>
    /// A request to be handled by <see cref="HttpRetryHandler"/>. This type should contain all
    /// of the knowledge necessary to make a request, while handling transient transport errors.
    /// </summary>
    public class HttpRetryHandlerRequest
    {
        public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromSeconds(60);

        public HttpRetryHandlerRequest(HttpClient httpClient, Func<HttpRequestMessage> requestFactory)
        {
            HttpClient = httpClient;
            RequestFactory = requestFactory;
            CompletionOption = HttpCompletionOption.ResponseHeadersRead;
            MaxTries = 3;
            RequestTimeout = TimeSpan.FromSeconds(100);
            RetryDelay = TimeSpan.FromMilliseconds(200);
            DownloadTimeout = DefaultDownloadTimeout;
        }

        /// <summary>The HTTP client to use for each request attempt.</summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// The factory that generates each request message. This factory is invoked for each attempt.
        /// </summary>
        public Func<HttpRequestMessage> RequestFactory { get; }

        /// <summary>The HTTP completion option to use for the next attempt.</summary>
        public HttpCompletionOption CompletionOption { get; set; }

        /// <summary>The maximum number of times to try the request. This value includes the initial attempt.</summary>
        /// <remarks>This API is intended only for testing purposes and should not be used in product code.</remarks>
        public int MaxTries { get; set; }

        /// <summary>How long to wait on the request to come back with a response.</summary>
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>How long to wait before trying again after a failed request.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public TimeSpan RetryDelay { get; set; }

        /// <summary>The timeout to apply to <see cref="DownloadTimeoutStream"/> instances.</summary>
        public TimeSpan DownloadTimeout { get; set; }
    }
}
