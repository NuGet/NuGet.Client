// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Security.Cryptography;
using System.Linq;
using System.Diagnostics;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    enum NuGetVerificationCertificateType
    {
        Signature,
        Timestamp
    }

    public class SignatureTrustAndValidityVerificationProvider : ISignatureVerificationProvider
    {
        private SigningSpecifications _specification => SigningSpecifications.V1;

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, CancellationToken token)
        {
            var result = VerifyValidityAndTrust(package, signature);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyValidityAndTrust(ISignedPackageReader package, Signature signature)
        {
            var status = SignatureVerificationStatus.Trusted;
            var issues = new List<SignatureLog>();
            var authorUnsignedAttributes = signature.SignerInfo.UnsignedAttributes;
            var timestampCms = new SignedCms();
            var dateTimeToCheckRevocation = DateTime.Now;

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, signature.Type.ToString())));

            foreach (var attribute in authorUnsignedAttributes)
            {
                if (string.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttributeOid))
                {
                    timestampCms.Decode(attribute.Values[0].RawData);

                    using (var authorSignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                    {
                        var signatureHash = NativeCms.GetSignatureValueHash(signature.SignatureManifest.HashAlgorithm, authorSignatureNativeCms);

                        status = VerifyTimestamp(timestampCms, signature.SignerInfo.Certificate, signatureHash, issues);
                        if (status == SignatureVerificationStatus.Invalid)
                        {
                            return new SignedPackageVerificationResult(status, signature, issues);
                        }

                        dateTimeToCheckRevocation = Rfc3161TimestampVerificationUtility.GetUpperLimit(timestampCms).ToLocalTime().DateTime;
                    }
                }
            }

            status = VerifySignatureValidity(signature, dateTimeToCheckRevocation, issues);

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationStatus VerifySignatureValidity(Signature signature, DateTime verificationTime, List<SignatureLog> issues)
        {
            var certificate = signature.SignerInfo.Certificate;
            if (certificate == null)
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorNoCertificate));
                issues.Add(SignatureLog.DebugLog(Strings.DebugNoCertificate));
                return SignatureVerificationStatus.Invalid;
            }

            if (signature.Type == SignatureType.Unknown)
            {
                issues.Add(SignatureLog.TrustOfSignatureCannotBeProvenWarning(Strings.WarningUnknownSignatureType));
                return SignatureVerificationStatus.Untrusted;
            }

            if (!SigningUtility.CertificateContainsEku(certificate, Oids.CodeSigningEkuOid))
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorCertificateNotCodeSigning));
                return SignatureVerificationStatus.Invalid;
            }

            if (!SigningUtility.IsCertificatePublicKeyValid(signature.SignerInfo.Certificate))
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorInvalidPublicKey));
                return SignatureVerificationStatus.Invalid;
            }

            if (SigningUtility.CertificateContainsEku(certificate, Oids.LifetimeSignerEkuOid))
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorCertificateHasLifetimeSignerEKU));
                return SignatureVerificationStatus.Invalid;
            }

            try
            {
                signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorSignatureVerificationFailed));
                issues.Add(SignatureLog.DebugLog(e.ToString()));
                return SignatureVerificationStatus.Invalid;
            }

            return VerifyCertificateChain(certificate, signature.SignedCms.Certificates, verificationTime, NuGetVerificationCertificateType.Signature, issues);
        }

        /// <summary>
        /// Validates a SignedCms object containing a timestamp response.
        /// </summary>
        /// <param name="timestampCms">SignedCms response from the timestamp authority.</param>
        /// <param name="signerCertificate">X509Certificate2 used to sign the data that was timestamped.</param>
        /// <param name="data">byte[] data that was signed and timestamped.</param>
        private SignatureVerificationStatus VerifyTimestamp(SignedCms timestampCms, X509Certificate2 signerCertificate, byte[] data, List<SignatureLog> issues)
        {
            if (!Rfc3161TimestampVerificationUtility.TryReadTSTInfoFromSignedCms(timestampCms, out var tstInfo))
            {
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidContentType)));
                return SignatureVerificationStatus.Invalid;
            }

            if (!tstInfo.HasMessageHash(data))
            {
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHash)));
                return SignatureVerificationStatus.Invalid;
            }

            if (!Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signerCertificate, tstInfo))
            {
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(Strings.TimestampFailureAuthorCertNotValid));
                return SignatureVerificationStatus.Invalid;
            }

            if (!_specification.AllowedHashAlgorithmOids.Contains(timestampCms.SignerInfos[0].DigestAlgorithm.Value))
            {
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHashAlgorithmOid)));
                return SignatureVerificationStatus.Invalid;
            }

            var timestamperCertificate = timestampCms.SignerInfos[0].Certificate;

            if (!SigningUtility.CertificateContainsEku(timestamperCertificate, Oids.TimeStampingEkuOid))
            {
                issues.Add(SignatureLog.InvalidTimestampInSignatureError(string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureCertInvalidEku)));
                return SignatureVerificationStatus.Invalid;
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.TimestampValue,
                tstInfo.Timestamp.LocalDateTime.ToString()) + Environment.NewLine));

            return VerifyCertificateChain(timestamperCertificate, timestampCms.Certificates, DateTime.Now, NuGetVerificationCertificateType.Timestamp, issues);
        }

        private SignatureVerificationStatus VerifyCertificateChain(X509Certificate2 certificate,
            X509Certificate2Collection additionalCertificates,
            DateTime verificationTime,
            NuGetVerificationCertificateType certificateType,
            List<SignatureLog> issues)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var result = SignatureVerificationStatus.Invalid;

            using (var chain = new X509Chain())
            {
                var certificateDisplayFormat = Strings.VerificationDefaultCertDisplay;
                var policy = chain.ChainPolicy;

                if (certificateType == NuGetVerificationCertificateType.Signature)
                {
                    policy.ApplicationPolicy.Add(new Oid(Oids.CodeSigningEkuOid));
                    certificateDisplayFormat = Strings.VerificationAuthorCertDisplay;
                }
                else if (certificateType == NuGetVerificationCertificateType.Timestamp)
                {
                    policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEkuOid));
                    policy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
                    certificateDisplayFormat = Strings.VerificationTimestamperCertDisplay;
                }

                policy.ExtraStore.AddRange(additionalCertificates);

                policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                policy.RevocationMode = X509RevocationMode.Online;

                policy.VerificationTime = verificationTime;

                if (chain.Build(certificate))
                {
                    result = SignatureVerificationStatus.Trusted;
                    issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                                certificateDisplayFormat,
                                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(certificate)}")));
                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain)));
                }

                foreach (var chainStatus in chain.ChainStatus)
                {
                    switch (chainStatus.Status)
                    {
                        case X509ChainStatusFlags.Revoked:
                            result = SignatureVerificationStatus.Invalid;
                            issues.Add(SignatureLog.InvalidPackageError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString())));
                            break;

                        case X509ChainStatusFlags.PartialChain:
                        case X509ChainStatusFlags.UntrustedRoot:
                            result = SignatureVerificationStatus.Untrusted;
                            issues.Add(SignatureLog.UntrustedRootError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString())));
                            break;

                        case X509ChainStatusFlags.OfflineRevocation:
                        case X509ChainStatusFlags.RevocationStatusUnknown:
                            result = SignatureVerificationStatus.Untrusted;
                            issues.Add(SignatureLog.TrustOfSignatureCannotBeProvenWarning(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString())));
                            break;

                        case X509ChainStatusFlags.NoError:
                            break;

                        default:
                            issues.Add(SignatureLog.InvalidPackageError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString())));
                            break;
                    }
                }

                return result;
            }
        }
#else
        private PackageVerificationResult VerifyValidityAndTrust(ISignedPackageReader package, Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
