// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using NuGet.Common;

namespace NuGet.Protocol
{
    internal static class HttpRequestMessageExtensions
    {
        public static readonly string NuGetLoggerKey = "NuGet_Logger";

        /// <summary>
        /// Clones an <see cref="HttpRequestMessage" /> request.
        /// </summary>
        public static HttpRequestMessage Clone(this HttpRequestMessage request)
        {
            Debug.Assert(request.Content == null, "Cloning the request content is not yet implemented.");

            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property);
            }

            return clone;
        }

        /// <summary>
        /// Retrieves a logger instance attached to the given request as custom property.
        /// </summary>
        /// <param name="request">Request message</param>
        /// <returns>Logger instance if exists, or null otherwise.</returns>
        public static ILogger GetLogger(this HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.GetProperty<ILogger>(NuGetLoggerKey);
        }

        /// <summary>
        /// Attaches a logger instance to the given request message as custom property.
        /// </summary>
        /// <param name="request">A request message</param>
        /// <param name="logger">A logger instance</param>
        public static void SetLogger(this HttpRequestMessage request, ILogger logger)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            request.Properties[NuGetLoggerKey] = logger;
        }

        private static T GetProperty<T>(this HttpRequestMessage request, string key)
        {
            object result;
            if (request.Properties.TryGetValue(key, out result) && result is T)
            {
                return (T)result;
            }

            return default(T);
        }
    }
}
