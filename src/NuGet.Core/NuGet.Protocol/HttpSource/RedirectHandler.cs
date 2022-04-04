// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if !IS_CORECLR

#nullable enable

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol
{
    // Handles redirections when automatic redirections have been turned off
    // Reference https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/RedirectHandler.cs
    internal class RedirectHandler : DelegatingHandler
    {
        private readonly int _maxRedirectsAllowed;

        public RedirectHandler(HttpClientHandler clientHandler)
            : base(clientHandler)
        {
            _maxRedirectsAllowed = clientHandler.MaxAutomaticRedirections;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            uint redirectCount = 0;
            Uri? redirectUri;

            var configuration = request.GetOrCreateConfiguration();

            while ((redirectUri = GetUriForRedirect(request.RequestUri, response, configuration.Logger)) != null)
            {
                redirectCount++;

                if (redirectCount > _maxRedirectsAllowed)
                {
                    break;
                }

                response.Dispose();

                // Clear the authorization header.
                request.Headers.Authorization = null;

                // Set up for the redirect
                request.RequestUri = redirectUri;
                if (RequestRequiresForceGet(response.StatusCode, request.Method))
                {
                    request.Method = HttpMethod.Get;
                    request.Content = null;
                    if (request.Headers.TransferEncodingChunked == true)
                    {
                        request.Headers.TransferEncodingChunked = false;
                    }
                }

                // Issue the redirected request.
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }

        private Uri? GetUriForRedirect(Uri requestUri, HttpResponseMessage response, ILogger logger)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Moved:
                case HttpStatusCode.Found:
                case HttpStatusCode.SeeOther:
                case HttpStatusCode.TemporaryRedirect:
                case HttpStatusCode.MultipleChoices:
                    break;

                default:
                    return null;
            }

            Uri? location = response.Headers.Location;
            if (location == null)
            {
                return null;
            }

            // Ensure the redirect location is an absolute URI.
            if (!location.IsAbsoluteUri)
            {
                location = new Uri(requestUri, location);
            }

            // Per https://tools.ietf.org/html/rfc7231#section-7.1.2, a redirect location without a
            // fragment should inherit the fragment from the original URI.
            string requestFragment = requestUri.Fragment;
            if (!string.IsNullOrEmpty(requestFragment))
            {
                string redirectFragment = location.Fragment;
                if (string.IsNullOrEmpty(redirectFragment))
                {
                    location = new UriBuilder(location) { Fragment = requestFragment }.Uri;
                }
            }

            // Disallow automatic redirection from secure to non-secure schemes
            if (requestUri.Scheme == Uri.UriSchemeHttps && location.Scheme == Uri.UriSchemeHttp)
            {
                logger?.LogError(string.Format(CultureInfo.CurrentCulture,
                                    Strings.Error_InsecureRedirectionBlocked,
                                    requestUri.ToString(),
                                    location.ToString()));
                return null;
            }

            return location;
        }

        private static bool RequestRequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod)
        {
            switch (statusCode)
            {
                case HttpStatusCode.Moved:
                case HttpStatusCode.Found:
                case HttpStatusCode.MultipleChoices:
                    return requestMethod == HttpMethod.Post;
                case HttpStatusCode.SeeOther:
                    return requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head;
                default:
                    return false;
            }
        }
    }
}
#endif
