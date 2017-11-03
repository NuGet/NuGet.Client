// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

using System.Text;
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
        private readonly Uri _timestamperUrl;
        private const int _rfc3161RequestTimeoutSeconds = 10;
        private const string _signingCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";


        public Rfc3161TimestampProvider(Uri timeStampServerUrl)
        {
#if IS_DESKTOP
            // Uri.UriSchemeHttp and Uri.UriSchemeHttps are not available in netstandard 1.3
            if (!string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                !string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid scheme for {nameof(timeStampServerUrl)}: {timeStampServerUrl}. The supported schemes are {Uri.UriSchemeHttp} and {Uri.UriSchemeHttps}.");
            }
#endif
            _timestamperUrl = timeStampServerUrl ?? throw new ArgumentNullException(nameof(timeStampServerUrl));
        }


#if IS_DESKTOP
        /// <summary>
        /// Timestamp a signature.
        /// </summary>
        public Task<Signature> CreateSignatureAsync(TimestampRequest request, ILogger logger, CancellationToken token)
        {
            // Get the signatureValue from the signerInfo object
            using (var nativeCms = NativeCms.Decode(request.Signature.GetBytes(), detached: false))
            {
                var signatureValueHashByteArray = GetSignatureValueHash(
                    request.TimestampHashAlgorithm,
                    nativeCms);

                // Allows us to track the request.
                var nonce = GenerateNonce();

                var rfc3161TimestampRequest = new Rfc3161TimestampRequest(
                signatureValueHashByteArray,
                request.TimestampHashAlgorithm.ConvertToSystemSecurityHashAlgorithmName(),
                nonce: nonce,
                requestSignerCertificates: true);

                // Request a timestamp
                var timestampToken = rfc3161TimestampRequest.SubmitRequest(
                    _timestamperUrl,
                    TimeSpan.FromSeconds(_rfc3161RequestTimeoutSeconds));

                // Verify the response
                var tokenCms = timestampToken.AsSignedCms();

                // TODO Check if there can be more than 1?
                var tokenSigner = tokenCms.SignerInfos[0];

                ValidateTimestampResponse(request, signatureValueHashByteArray, nonce, timestampToken, tokenSigner);

                var signingCertificate = GetSigningCertificateFromTimestampResponse(tokenSigner);

                //TODO validate the certificate chain.

                // Returns the signature as a Signature object
                return Task.FromResult(Signature.Load(tokenCms));
            }
        }

        private static CryptographicAttributeObject GetSigningCertificateFromTimestampResponse(SignerInfo tokenSigner)
        {
            CryptographicAttributeObject signingCertificateV2 = null;

            foreach (var attr in tokenSigner.SignedAttributes)
            {
                if (string.Equals(attr.Oid.Value, _signingCertificateV2Oid))
                {
                    signingCertificateV2 = attr;
                    break;
                }
            }

            if (signingCertificateV2 == null)
            {
                throw new InvalidOperationException("Rfc3161TimestampToken does not contain a signer certificate.");
            }

            return signingCertificateV2;
        }

        private static void ValidateTimestampResponse(TimestampRequest request,
            byte[] signatureValueHashByteArray,
            byte[] nonce,
            Rfc3161TimestampToken timestampToken,
            SignerInfo tokenSigner)
        {
            if (!timestampToken.TokenInfo.HasMessageHash(signatureValueHashByteArray))
            {
                throw new InvalidOperationException($"Rfc3161TimestampToken contains invalid {nameof(signatureValueHashByteArray)}.");
            }

            if (!nonce.SequenceEqual(timestampToken.TokenInfo.GetNonce()))
            {
                throw new InvalidOperationException($"Rfc3161TimestampToken contains invalid {nameof(nonce)}.");
            }

            if (!request.SigningSpec.AllowedHashAlgorithmOids.Contains(tokenSigner.DigestAlgorithm.Value))
            {
                throw new InvalidOperationException("Rfc3161TimestampToken contains invalid hash algorithm Oid.");
            }
        }

        private static byte[] GetSignatureValueHash(Common.HashAlgorithmName hashAlgorithm, NativeCms nativeCms)
        {
            var signatureValue = nativeCms.GetEncryptedDigest();

            var signatureValueStream = new MemoryStream(signatureValue);

            var signatureValueHashByteArray = hashAlgorithm
                .GetHashProvider()
                .ComputeHash(signatureValueStream, leaveStreamOpen: false);

            return signatureValueHashByteArray;
        }

        private static byte[] GenerateNonce()
        {
            var nonce = new byte[24];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            return nonce;
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
