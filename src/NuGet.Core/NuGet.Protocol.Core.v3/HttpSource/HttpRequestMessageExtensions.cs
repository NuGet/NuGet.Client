// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;

namespace NuGet.Protocol
{
    public static class HttpRequestMessageExtensions
    {
        private static readonly string NuGetConfigurationKey = "NuGet_Configuration";

        /// <summary>
        /// Clones an <see cref="HttpRequestMessage" /> request.
        /// </summary>
        internal static HttpRequestMessage Clone(this HttpRequestMessage request)
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
        /// Retrieves the HTTP request configuration instance attached to the given message as custom property.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <returns>Configuration instance if exists, or a default instance otherwise.</returns>
        public static HttpRequestMessageConfiguration GetOrCreateConfiguration(this HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var foundInstance = request.GetProperty<HttpRequestMessageConfiguration>(NuGetConfigurationKey);

            return foundInstance ?? HttpRequestMessageConfiguration.Default;
        }

        /// <summary>
        /// Attaches an HTTP request configuration instance to the given message as custom property.
        /// If the configuration has already been set on the request message, the old configuration
        /// is replaced.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <param name="configuration">An HTTP request message configuration instance.</param>
        public static void SetConfiguration(this HttpRequestMessage request, HttpRequestMessageConfiguration configuration)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            request.Properties[NuGetConfigurationKey] = configuration;
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
