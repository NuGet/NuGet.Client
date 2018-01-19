// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

using System.Security.Cryptography.X509Certificates;
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

        public Rfc3161TimestampProvider(Uri timeStampServerUrl)
        {
#if IS_DESKTOP
            // Uri.UriSchemeHttp and Uri.UriSchemeHttps are not available in netstandard 1.3
            if (!string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                !string.Equals(timeStampServerUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.TimestampFailureInvalidHttpScheme,
                    timeStampServerUrl,
                    nameof(Uri.UriSchemeHttp),
                    nameof(Uri.UriSchemeHttps)));
            }
#endif
            _timestamperUrl = timeStampServerUrl ?? throw new ArgumentNullException(nameof(timeStampServerUrl));
        }


#if IS_DESKTOP

        /// <summary>
        /// Timestamps data present in the TimestampRequest.
        /// </summary>
        public Task<Signature> TimestampSignatureAsync(TimestampRequest request, ILogger logger, CancellationToken token)
        {
            var timestampedSignature = TimestampData(request, logger, token);
            return Task.FromResult(Signature.Load(timestampedSignature));
        }

        /// <summary>
        /// Timestamps data present in the TimestampRequest.
        /// </summary>
        public byte[] TimestampData(TimestampRequest request, ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Get the signatureValue from the signerInfo object
            using (var signatureNativeCms = NativeCms.Decode(request.SignatureValue, detached: false))
            {
                var signatureValueHashByteArray = NativeCms.GetSignatureValueHash(
                    request.TimestampHashAlgorithm,
                    signatureNativeCms);

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

                // quick check for response validity
                ValidateTimestampResponse(nonce, signatureValueHashByteArray, timestampToken);

                var timestampCms = timestampToken.AsSignedCms();
                ValidateTimestampCms(request.SigningSpec, timestampCms);

                byte[] timestampByteArray;

                using (var timestampNativeCms = NativeCms.Decode(timestampCms.Encode(), detached: false))
                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;
                    var policy = chain.ChainPolicy;

                    policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
                    policy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                    policy.ExtraStore.AddRange(timestampCms.Certificates);

                    policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    policy.RevocationMode = X509RevocationMode.Online;

                    var timestampSignerCertificate = timestampCms.SignerInfos[0].Certificate;

                    if (timestampSignerCertificate == null)
                    {
                        throw new TimestampException(NuGetLogCode.NU3020, Strings.TimestampNoCertificate);
                    }

                    if (!chain.Build(timestampSignerCertificate))
                    {
                        var messages = CertificateChainUtility.GetMessagesFromChainStatuses(chain.ChainStatus);

                        throw new TimestampException(NuGetLogCode.NU3028, string.Format(CultureInfo.CurrentCulture, Strings.TimestampCertificateChainBuildFailure, string.Join(", ", messages)));
                    }

                    // Insert all the certificates into timestampCms
                    InsertTimestampCertChainIntoTimestampCms(timestampCms, chain, timestampNativeCms);
                    timestampByteArray = timestampNativeCms.Encode();
                }

                signatureNativeCms.AddTimestamp(timestampByteArray);

                return signatureNativeCms.Encode();
            }
        }

        private static void ValidateTimestampCms(SigningSpecifications spec, SignedCms timestampCms)
        {
            var signerInfo = timestampCms.SignerInfos[0];
            try
            {
                signerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch(Exception e)
            {
                throw new TimestampException(NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed, e);
            }

            if (!CertificateUtility.IsSignatureAlgorithmSupported(signerInfo.Certificate))
            {
                throw new TimestampException(NuGetLogCode.NU3022, Strings.TimestampUnsupportedSignatureAlgorithm);
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(signerInfo.Certificate))
            {
                throw new TimestampException(NuGetLogCode.NU3023, Strings.TimestampCertificateFailsPublicKeyLengthRequirement);
            }

            if (!spec.AllowedHashAlgorithmOids.Contains(signerInfo.DigestAlgorithm.Value))
            {
                throw new TimestampException(NuGetLogCode.NU3024, Strings.TimestampUnsupportedSignatureAlgorithm);
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(signerInfo.Certificate))
            {
                throw new TimestampException(NuGetLogCode.NU3025, Strings.TimestampNotYetValid);
            }
        }

        private static void ValidateTimestampResponse(byte[] nonce, byte[] data, Rfc3161TimestampToken timestampToken)
        {
            if (!nonce.SequenceEqual(timestampToken.TokenInfo.GetNonce()))
            {
                throw new TimestampException(NuGetLogCode.NU3026, Strings.TimestampFailureNonceMismatch);
            }

            if (!timestampToken.TokenInfo.HasMessageHash(data))
            {
                throw new TimestampException(NuGetLogCode.NU3019, Strings.TimestampIntegrityCheckFailed);
            }
        }

        private static void InsertTimestampCertChainIntoTimestampCms(
            SignedCms timestampCms,
            X509Chain timstampCertChain,
            NativeCms timestampNativeCms)
        {
            timestampNativeCms.AddCertificates(timstampCertChain.ChainElements
                .Cast<X509ChainElement>()
                .Where(c => !timestampCms.Certificates.Contains(c.Certificate))
                .Select(c => c.Certificate.Export(X509ContentType.Cert)));
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