// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif

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
        private const long _ticksPerMicroSecond = 10;
        

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
        /// Timestamps a Signature present in the TimestampRequest.
        /// </summary>
        public Task<Signature> TimestampSignatureAsync(TimestampRequest request, ILogger logger, CancellationToken token)
        {
            // Get the signatureValue from the signerInfo object
            using (var signatureNativeCms = NativeCms.Decode(request.Signature.GetBytes(), detached: false))
            {
                var signatureValueHashByteArray = GetSignatureValueHash(
                    request.TimestampHashAlgorithm,
                    signatureNativeCms);

                if (signatureValueHashByteArray.Count() == 0)
                {
                    throw new InvalidOperationException("Signature hash value is empty");
                }

                // Allows us to track the request.
                var nonce = GenerateNonce();

                var rfc3161TimestampRequest = new Rfc3161TimestampRequest(
                signatureValueHashByteArray,
                request.TimestampHashAlgorithm.ConvertToSystemSecurityHashAlgorithmName(),
                nonce: nonce,
                requestSignerCertificates: true);

                // Request a timestamp
                // The response status need not be checked here as lower level api will throw if the response is invalid
                var timestampToken = rfc3161TimestampRequest.SubmitRequest(
                    _timestamperUrl,
                    TimeSpan.FromSeconds(_rfc3161RequestTimeoutSeconds));

                // Verify the response
                var timestampCms = timestampToken.AsSignedCms();

                // Taking the first timestamp signer is acceptable
                var timestampSigner = timestampCms.SignerInfos[0];

                ValidateTimestampResponse(request, signatureValueHashByteArray, nonce, timestampToken, timestampSigner);

                var signerCert = timestampSigner.Certificate;

                var timstampCertChain = ValidateTimestampCertificate(signerCert, timestampCms);

                ValidateSignerCertificateAgainstTimestamp(request, timestampToken);

                byte[] timestampByteArray;

                // Insert all the certificates into timestampCms
                using (var timestampNativeCms = NativeCms.Decode(timestampCms.Encode(), detached: false))
                {
                    InsertTimestamperCertificateChainIntoTimestampCms(timestampCms, timstampCertChain, timestampNativeCms);
                    timestampByteArray = timestampCms.Encode();
                }

                if (timestampByteArray.Count() == 0)
                {
                    throw new InvalidOperationException("Timestamp Byte Array is empty");
                }

                signatureNativeCms.AddTimestamp(timestampByteArray);

                // Convert a nativeCms object into a Signature object
                 return Task.FromResult(Signature.Load(signatureNativeCms.Encode()));
            }
            
        }

        private static void InsertTimestamperCertificateChainIntoTimestampCms(SignedCms timestampCms, X509Chain timstampCertChain, NativeCms timestampNativeCms)
        {
            timestampNativeCms.AddCertificates(timstampCertChain.ChainElements
                .Cast<X509ChainElement>()
                .Where(c => !timestampCms.Certificates.Contains(c.Certificate))
                .Select(c => c.Certificate.Export(X509ContentType.Cert)));
        }

        private static void ValidateSignerCertificateAgainstTimestamp(TimestampRequest request, Rfc3161TimestampToken timestampToken)
        {
            var tstInfoGenTime = timestampToken.TokenInfo.Timestamp;
            var tstInfoAccuracy = timestampToken.TokenInfo.AccuracyInMicroseconds;
            long tstInfoAccuracyInTicks;

            if (!tstInfoAccuracy.HasValue)
            {
                if (string.Equals(timestampToken.TokenInfo.PolicyId, Oids.BaselineTimestampPolicyOid))
                {
                    tstInfoAccuracyInTicks = TimeSpan.TicksPerSecond;
                }
                else
                {
                    tstInfoAccuracyInTicks = 0;
                }
            }
            else
            {
                tstInfoAccuracyInTicks = tstInfoAccuracy.Value * _ticksPerMicroSecond;
            }

            var timestampUpperGenTime = tstInfoGenTime.AddTicks(tstInfoAccuracyInTicks);
            var timestampLowerGenTime = tstInfoGenTime.Subtract(TimeSpan.FromTicks(tstInfoAccuracyInTicks));

            if (request.Certificate.NotAfter < timestampUpperGenTime ||
                request.Certificate.NotBefore > timestampLowerGenTime)
            {
                throw new InvalidOperationException("Author's certificate was not valid when it was timestamped.");
            }
        }

        private static X509Chain ValidateTimestampCertificate(X509Certificate2 signerCert, SignedCms tokenCms)
        {
            //validate the certificate chain.
            X509Chain chain = null;

            if (!SigningUtility.IsCertificateValid(signerCert, out chain, allowUntrustedRoot: false, checkRevocationStatus: true))
            {
                //TODO throw better error message
                throw new InvalidOperationException("The timestamper's certificate chain does not build.");
            }

            if (!SigningUtility.CertificateContainsEku(signerCert, Oids.TimeStampingEkuOid))
            {
                throw new InvalidOperationException("The timestamper's certificate does not contain a valid EKU for timestamping.");
            }

            return chain;
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
        public Task<Signature> TimestampSignatureAsync(TimestampRequest timestampRequest, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
