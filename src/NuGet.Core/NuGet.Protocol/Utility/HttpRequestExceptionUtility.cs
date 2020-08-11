// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Http;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    internal static class HttpRequestExceptionUtility
    {
        internal static HttpStatusCode? GetHttpStatusCode(HttpRequestException ex)
        {
            HttpStatusCode? statusCode = null;

#if NETCOREAPP5_0
            statusCode = ex.StatusCode;
#else
            // All places which might raise an HttpRequestException need to put StatusCode in exception object.
            if (ex.Data.Contains(StatusCode))
            {
                statusCode = (HttpStatusCode)ex.Data[StatusCode];
            }
#endif

            return statusCode;
        }

        internal static void ThrowFatalProtocolExceptionIfCritical(HttpRequestException ex, string url)
        {
            HttpStatusCode? statusCode = GetHttpStatusCode(ex);
            NuGetLogCode? logCode = null;
            string message = null;

            // For these status codes, we throw exception.
            // Ideally, we add more status codes to this switch statement as we run into other codes that
            // will benefit with a better error experience.
            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForUnauthorized, url);
                    logCode = NuGetLogCode.NU1301;
                    break;
                case HttpStatusCode.Forbidden:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForForbidden, url);
                    logCode = NuGetLogCode.NU1303;
                    break;
                case HttpStatusCode.NotFound:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_UrlNotFound, url);
                    logCode = NuGetLogCode.NU1304;
                    break;
                case HttpStatusCode.ProxyAuthenticationRequired:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForProxy, url);
                    logCode = NuGetLogCode.NU1307;
                    break;
            }

            if (message != null)
            {
                throw new FatalProtocolException(message, ex, logCode.Value);
            }
        }

        internal static void EnsureSuccessAndStashStatusCodeIfThrows(HttpResponseMessage response)
        {
#if !NETCOREAPP5_0
            // Before calling EnsureSuccessStatusCode(), squirrel away statuscode, in order to add it to exception.
            HttpStatusCode statusCode = response.StatusCode;
            try
            {
#endif
                response.EnsureSuccessStatusCode();
#if !NETCOREAPP5_0
            }
            catch (HttpRequestException ex)
            {
                ex.Data[StatusCode] = statusCode;
                throw;
            }
#endif
        }

#if !NETCOREAPP5_0
        private const string StatusCode = "StatusCode";
#endif
    }
}
