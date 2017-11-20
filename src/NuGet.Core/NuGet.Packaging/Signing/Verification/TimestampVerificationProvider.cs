// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class TimestampVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, CancellationToken token)
        {
            var result = VerifySignature(package, signature);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifySignature(ISignedPackageReader package, Signature signature)
        {
            var status = SignatureVerificationStatus.Trusted;
            var signatureIssues = new List<SignatureLog>();
            var issues = new List<SignatureLog>();

            var authorUnsignedAttributes = signature.SignerInfo.UnsignedAttributes;
            var timestampCms = new SignedCms();

            try
            {
                foreach (var attribute in authorUnsignedAttributes)
                {
                    if (string.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttributeOid))
                    {
                        timestampCms.Decode(attribute.Values[0].RawData);

                        using (var authorSignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                        {
                            var signatureHash = NativeCms.GetSignatureValueHash(signature.SignatureManifest.HashAlgorithm, authorSignatureNativeCms);

                            Validate(timestampCms, SigningSpecifications.V1, signature.SignerInfo.Certificate, signatureHash, issues);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                status = SignatureVerificationStatus.Invalid;
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(Strings.TimestampInvalid));
                issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedWithException, nameof(TimestampVerificationProvider), e.Message)));
            }

            return new TimestampedPackageVerificationResult(status, signature, issues);
        }


        /// <summary>
        /// Validates a SignedCms object containing a timestamp response.
        /// </summary>
        /// <param name="timestampCms">SignedCms response from the timestamp authority.</param>
        /// <param name="specifications">SigningSpecifications used to validate allowed hash algorithms.</param>
        /// <param name="signerCertificate">X509Certificate2 used to sign the data that was timestamped.</param>
        /// <param name="data">byte[] data that was signed and timestamped.</param>
        private static void Validate(SignedCms timestampCms, SigningSpecifications specifications, X509Certificate2 signerCertificate, byte[] data, List<SignatureLog> issues)
        {
            if (!Rfc3161TimestampVerifier.ValidateTimestampAlgorithm(timestampCms, specifications))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHashAlgorithmOid)));
            }

            Rfc3161TimestampTokenInfo tstInfo;

            if (!Rfc3161TimestampVerifier.TryReadTSTInfoFromSignedCms(timestampCms, out tstInfo))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidContentType)));
            }

            if (!Rfc3161TimestampVerifier.ValidateTimestampedData(tstInfo, data))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHash)));
            }

            if (!Rfc3161TimestampVerifier.ValidateSignerCertificateAgainstTimestamp(signerCertificate, tstInfo))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3012,
                    Strings.TimestampFailureAuthorCertNotValid));
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.TimestampValue,
                tstInfo.Timestamp.LocalDateTime.ToString()) + Environment.NewLine));

            var timestamperCertificate = timestampCms.SignerInfos[0].Certificate;

            if (!Rfc3161TimestampVerifier.ValidateTimestampEnhancedKeyUsage(timestamperCertificate))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureCertInvalidEku)));
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationTimestamperCertDisplay,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestamperCertificate)}")));

            if (!Rfc3161TimestampVerifier.TryBuildTimestampCertificateChain(timestamperCertificate, timestampCms.Certificates, out var chain))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3011,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampCertificateChainBuildFailure,
                    timestamperCertificate.FriendlyName)));
            }

            if (chain != null)
            {
                issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain)));
            }
        }

#else
        private PackageVerificationResult VerifySignature(ISignedPackageReader package, Signature signature)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
