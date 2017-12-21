// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class VerificationUtility
    {
        internal static bool IsSigningCertificateValid(X509Certificate2 certificate, bool failIfInvalid, List<SignatureLog> issues)
        {
            var isValid = true;

            if (!CertificateUtility.IsSignatureAlgorithmSupported(certificate))
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3013, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm));
                isValid = false;
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(certificate))
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3014, Strings.SigningCertificateFailsPublicKeyLengthRequirement));
                isValid = false;
            }

            if (CertificateUtility.HasExtendedKeyUsage(certificate, Oids.LifetimeSignerEkuOid))
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3015, Strings.ErrorCertificateHasLifetimeSignerEKU));
                isValid = false;
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate))
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3017, Strings.SignatureNotYetValid));
                isValid = false;
            }

            return isValid;
        }

#if IS_DESKTOP
        internal static bool IsTimestampValid(Timestamp timestamp, byte[] data, bool failIfInvalid, List<SignatureLog> issues, SigningSpecifications spec)
        {
            var isValid = true;
            var signerInfo = timestamp.SignerInfo;

            if (!timestamp.TstInfo.HasMessageHash(data))
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3019, Strings.TimestampIntegrityCheckFailed));
                isValid = false;
            }

            if (timestamp.SignerInfo.Certificate != null)
            {
                try
                {
                    signerInfo.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception e)
                {
                    issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3021, Strings.ErrorTimestampVerificationFailed));
                    issues.Add(SignatureLog.DebugLog(e.ToString()));
                    isValid = false;
                }

                if (!CertificateUtility.IsSignatureAlgorithmSupported(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3022, Strings.TimestampCertificateHasUnsupportedSignatureAlgorithm));
                    isValid = false;
                }

                if (!CertificateUtility.IsCertificatePublicKeyValid(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3023, Strings.TimestampCertificateFailsPublicKeyLengthRequirement));
                    isValid = false;
                }

                if (!spec.AllowedHashAlgorithmOids.Contains(signerInfo.DigestAlgorithm.Value))
                {
                    issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3024, Strings.TimestampUnsupportedSignatureAlgorithm));
                    isValid = false;
                }

                if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(signerInfo.Certificate))
                {
                    issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3025, Strings.TimestampNotYetValid));
                    isValid = false;
                }
            }
            else
            {
                issues.Add(SignatureLog.Issue(failIfInvalid, NuGetLogCode.NU3020, Strings.TimestampNoCertificate));
                isValid = false;
            }

            return isValid;
        }
#endif
    }
}
