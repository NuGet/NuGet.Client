// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
#if IS_SIGNING_SUPPORTED && IS_CORECLR
    internal sealed class Rfc3161TimestampTokenNetstandard21Wrapper : IRfc3161TimestampToken
    {
        private readonly System.Security.Cryptography.Pkcs.Rfc3161TimestampToken _rfc3161TimestampToken;
        private readonly Lazy<IRfc3161TimestampTokenInfo> _tokenInfo;

        public Rfc3161TimestampTokenNetstandard21Wrapper(
            IRfc3161TimestampTokenInfo tstInfo,
            X509Certificate2 signerCertificate,
            X509Certificate2Collection additionalCerts,
            byte[] encoded)
        {
            bool success = System.Security.Cryptography.Pkcs.Rfc3161TimestampToken.TryDecode(
                new ReadOnlyMemory<byte>(encoded),
                out _rfc3161TimestampToken,
                out int bytesConsumed);

            if (!success)
            {
                throw new CryptographicException(Strings.InvalidAsn1);
            }

            _tokenInfo = new Lazy<IRfc3161TimestampTokenInfo>(
                () => new Rfc3161TimestampTokenInfoNetstandard21Wrapper(_rfc3161TimestampToken.TokenInfo));
        }

        public Rfc3161TimestampTokenNetstandard21Wrapper(
            System.Security.Cryptography.Pkcs.Rfc3161TimestampToken rfc3161TimestampToken)
        {
            _rfc3161TimestampToken = rfc3161TimestampToken;
        }

        public IRfc3161TimestampTokenInfo TokenInfo => _tokenInfo.Value;

        public SignedCms AsSignedCms()
        {
            return _rfc3161TimestampToken.AsSignedCms();
        }
    }
#endif
}

