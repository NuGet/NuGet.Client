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
        private const string _signingCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";
        private const string _timeStampingEkuOid = "1.3.6.1.5.5.7.3.8";
        private const string _baselineTimestampOid = "0.4.0.2023.1.1";
        private static readonly Oid _tstOid = new Oid("1.2.840.113549.1.9.16.2.14");
        private static readonly Oid _tstOid2 = new Oid("1.2.840.113549.1.9.16.2.17");

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
        public Task TimestampSignatureAsync(TimestampRequest request, ILogger logger, CancellationToken token)
        {
            byte[] signatureValueHashByteArray;

            // Get the signatureValue from the signerInfo object
            using (var nativeCms = NativeCms.Decode(request.Signature.GetBytes(), detached: false))
            {
                signatureValueHashByteArray = GetSignatureValueHash(
                    request.TimestampHashAlgorithm,
                    nativeCms);
            }

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
            var tokenCms = timestampToken.AsSignedCms();

            // Taking the first timestamp signer is acceptable
            var tokenSigner = tokenCms.SignerInfos[0];

            ValidateTimestampResponse(request, signatureValueHashByteArray, nonce, timestampToken, tokenSigner);

            var signerCert = tokenSigner.Certificate;

            ValidateTimestampCertificate(signerCert, tokenCms);

            ValidateSignerCertificateAgainstTimestamp(request, timestampToken);

            // updates the signature with the timestamp
            var index = InsertTimestampIntoSignature(request, timestampToken);

            return Task.FromResult(index);
            
        }

        private static int InsertTimestampIntoSignature(TimestampRequest request, Rfc3161TimestampToken timestampToken)
        {
            var asnData = new AsnEncodedData(_tstOid, timestampToken.GetEncodedValue());

            var asnDataCollection = new AsnEncodedDataCollection(asnData);

            var timestampAttribute = new CryptographicAttributeObject(
                asnData.Oid,
                asnDataCollection);

            var signingTime = new Pkcs9SigningTime();

            var res1 = request.Signature.SignerInfo.UnsignedAttributes.Add(timestampAttribute);

            return 0;
        }

        private static void ValidateSignerCertificateAgainstTimestamp(TimestampRequest request, Rfc3161TimestampToken timestampToken)
        {
            var tstInfoGenTime = timestampToken.TokenInfo.Timestamp;
            var tstInfoAccuracy = timestampToken.TokenInfo.AccuracyInMicroseconds;
            long tstInfoAccuracyInTicks;

            if (!tstInfoAccuracy.HasValue)
            {
                if (string.Equals(timestampToken.TokenInfo.PolicyId, _baselineTimestampOid))
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

        private static void ValidateTimestampCertificate(X509Certificate2 signerCert, SignedCms tokenCms)
        {
            //validate the certificate chain.
            X509Chain chain = null;

            if (!SigningUtility.IsCertificateValid(signerCert, out chain, allowUntrustedRoot: false, checkRevocationStatus: true))
            {
                //TODO throw better error message
                throw new InvalidOperationException("The timestamper's certificate chain does not build.");
            }

            if (!SigningUtility.CertificateContainsEku(signerCert, _timeStampingEkuOid))
            {
                throw new InvalidOperationException("The timestamper's certificate does not contain a valid EKU for timestamping.");
            }

            //TODO check if all the certificates are in the cms object
            // The 2 counts are not matching.
            //if (chain.ChainElements.Count != tokenCms.Certificates.Count)
            //{
            //    throw new InvalidOperationException("The timestamper's certificates count does not match the built chain count.");
            //}

            //foreach(var chainElement in chain.ChainElements)
            //{
            //    if (!tokenCms.Certificates.Contains(chainElement.Certificate))
            //    {
            //        throw new InvalidOperationException("The timestamper's certificates do not match the built chain.");
            //    }
            //}
        }

        //private static X509Certificate2 GetSignerCertFromTimestampResponse(SignerInfo tokenSigner)
        //{
        //    CryptographicAttributeObject signingCertificateV2 = null;

        //    foreach (var attr in tokenSigner.SignedAttributes)
        //    {
        //        if (string.Equals(attr.Oid.Value, _signingCertificateV2Oid))
        //        {
        //            signingCertificateV2 = attr;
        //            break;
        //        }
        //    }

        //    if (signingCertificateV2 == null)
        //    {
        //        throw new InvalidOperationException("Rfc3161TimestampToken does not contain a signer certificate.");
        //    }

        //    return tokenSigner.Certificate;
        //}

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
        public Task TimestampSignatureAsync(TimestampRequest timestampRequest, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
