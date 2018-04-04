// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public static class VerificationUtility
    {
        internal static SignatureVerificationStatusFlags ValidateSigningCertificate(X509Certificate2 certificate, bool treatIssuesAsErrors, string signatureType, List<SignatureLog> issues)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var validationFlags = SignatureVerificationStatusFlags.NoErrors;

            if (!CertificateUtility.IsSignatureAlgorithmSupported(certificate))
            {
                issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3013, string.Format(CultureInfo.CurrentCulture, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm, signatureType)));
                validationFlags |= SignatureVerificationStatusFlags.SignatureAlgorithmUnsupported;
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(certificate))
            {
                issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3014, string.Format(CultureInfo.CurrentCulture, Strings.SigningCertificateFailsPublicKeyLengthRequirement, signatureType)));
                validationFlags |= SignatureVerificationStatusFlags.CertificatePublicKeyInvalid;
            }

            if (CertificateUtility.HasExtendedKeyUsage(certificate, Oids.LifetimeSigningEku))
            {
                issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3015, string.Format(CultureInfo.CurrentCulture, Strings.VerifyErrorCertificateHasLifetimeSigningEKU, signatureType)));
                validationFlags |= SignatureVerificationStatusFlags.HasLifetimeSigningEku;
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate))
            {
                issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3017, string.Format(CultureInfo.CurrentCulture, Strings.SignatureNotYetValid, signatureType)));
                validationFlags |= SignatureVerificationStatusFlags.CertificateValidityInTheFuture;
            }

            return validationFlags;
        }

#if IS_DESKTOP
        internal static bool IsTimestampValid(Timestamp timestamp, Signature signature, bool treatIssuesAsErrors, List<SignatureLog> issues, SigningSpecifications spec)
        {
            if (timestamp == null)
            {
                throw new ArgumentNullException(nameof(timestamp));
            }
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            // Default to specification v1
            spec = spec ?? SigningSpecifications.V1;

            var isValid = true;
            var signerInfo = timestamp.SignerInfo;

            if (timestamp.SignerInfo.Certificate != null)
            {
                try
                {
                    signerInfo.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception e)
                {
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed));
                    issues.Add(SignatureLog.DebugLog(e.ToString()));
                    isValid = false;
                }

                if (!CertificateUtility.IsSignatureAlgorithmSupported(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3022, Strings.TimestampUnsupportedSignatureAlgorithm));
                    isValid = false;
                }

                if (!CertificateUtility.IsCertificatePublicKeyValid(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3023, Strings.TimestampCertificateFailsPublicKeyLengthRequirement));
                    isValid = false;
                }

                if (!spec.AllowedHashAlgorithmOids.Contains(signerInfo.DigestAlgorithm.Value))
                {
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3024, Strings.TimestampUnsupportedSignatureAlgorithm));
                    isValid = false;
                }

                try
                {
                    var hashAlgorithm = CryptoHashUtility.OidToHashAlgorithmName(timestamp.TstInfo.HashAlgorithmId.Value);
                    var signatureValue = signature.GetSignatureValue();
                    var messageHash = hashAlgorithm.ComputeHash(signatureValue);

                    if (!timestamp.TstInfo.HasMessageHash(messageHash))
                    {
                        issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3019, Strings.TimestampIntegrityCheckFailed));
                        isValid = false;
                    }
                }
                catch
                {
                    // If the hash algorithm is not supported OidToHashAlgorithmName will throw
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3030, Strings.TimestampMessageImprintUnsupportedHashAlgorithm));
                    isValid = false;
                }

                if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3025, Strings.TimestampNotYetValid));
                    isValid = false;
                }
            }
            else
            {
                issues.Add(SignatureLog.Issue(treatIssuesAsErrors, NuGetLogCode.NU3020, Strings.TimestampNoCertificate));
                isValid = false;
            }

            return isValid;
        }
#endif
    }
}