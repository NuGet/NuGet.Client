// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A provider for RFC 3161 timestamps
    /// https://tools.ietf.org/html/rfc3161
    /// </summary>
    public class Rfc3161TimestampProvider : ITimestampProvider
    {
        // Url to an RFC 3161 timestamp server
        private readonly Uri _url;

        public Rfc3161TimestampProvider(Uri timeStampServerUrl)
        {

#if IS_DESKTOP // Uri.UriSchemeHttp and Uri.UriSchemeHttps are not available in netstandard 1.3

            if (!string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                !string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid scheme for {nameof(timeStampServerUrl)}: {timeStampServerUrl}. The supported schemes are {Uri.UriSchemeHttp} and {Uri.UriSchemeHttps}.");
            }
#endif
            _url = timeStampServerUrl ?? throw new ArgumentNullException(nameof(timeStampServerUrl));
        }


#if IS_DESKTOP
        /// <summary>
        /// Timestamp a signature.
        /// </summary>
        public Task<Signature> CreateSignatureAsync(TimestampRequest timestampRequest, ILogger logger, CancellationToken token)
        {
            // Returns the signature as-is for now.
            return Task.FromResult(timestampRequest.Signature);
        }
#else
        /// <summary>
        /// Timestamp a signature.
        /// </summary>
        public Task<Signature> CreateSignatureAsync(TimestampRequest timestampRequest, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
