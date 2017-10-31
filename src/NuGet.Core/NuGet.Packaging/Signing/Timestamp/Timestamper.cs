// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class Timestamper
    {
        private readonly Uri _url;

        public Timestamper(Uri timeStamperUrl)
        {
            if (!string.Equals(timeStamperUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                !string.Equals(timeStamperUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid scheme for {nameof(timeStamperUrl)}: {timeStamperUrl}. The supported schemes are {Uri.UriSchemeHttp} and {Uri.UriSchemeHttps}.");
            }

            _url = timeStamperUrl ?? throw new ArgumentNullException(nameof(timeStamperUrl));
        }

        public Task TimeStampAsync(Signature signature)
        {

        }
    }
}
