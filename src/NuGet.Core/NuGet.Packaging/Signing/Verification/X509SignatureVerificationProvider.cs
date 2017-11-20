// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using System.Globalization;
using System.Diagnostics;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public class X509SignatureVerificationProvider : ISignatureVerificationProvider
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

            if (signature.Type == SignatureType.Unknown)
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.TrustOfSignatureCannotBeProvenWarning(Strings.WarningUnknownSignatureType));
                return new SignedPackageVerificationResult(status, signature, issues);
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, signature.Type.GetString())));

            try
            {
                signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorSignatureVerificationFailed));
                issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedWithException, nameof(signature.SignerInfo.CheckSignature), e.Message)));
            }

            if (signature.SignerInfo.Certificate == null)
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorNoCertificate));
                issues.Add(SignatureLog.DebugLog(Strings.DebugNoCertificate));
                return new SignedPackageVerificationResult(status, signature, issues);
            }

            issues.Add(SignatureLog.InformationLog(Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationAuthorCertDisplay,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(signature.SignerInfo.Certificate)}")));

            status = GetVerificationStatusFromCertificate(signature.SignerInfo.Certificate, signature.SignedCms.Certificates, issues);

            if (!SigningUtility.IsCertificatePublicKeyValid(signature.SignerInfo.Certificate))
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorInvalidPublicKey));
                status = SignatureVerificationStatus.Invalid;
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationStatus GetVerificationStatusFromCertificate(X509Certificate2 certificate, X509Certificate2Collection additionalCertificates, List<SignatureLog> issues)
        {
            X509Chain chain = null;
            var status = SignatureVerificationStatus.Untrusted;

            try
            {
                if (SigningUtility.IsCertificateValid(certificate, additionalCertificates, out chain, allowUntrustedRoot: false, checkRevocationMode: X509RevocationMode.Online))
                {
                    status = SignatureVerificationStatus.Trusted;
                }
                else if (SigningUtility.IsCertificateValid(certificate, additionalCertificates, out chain, allowUntrustedRoot: false, checkRevocationMode: X509RevocationMode.Offline))
                {
                    issues.Add(SignatureLog.UntrustedRootWarning(Strings.WarningOfflineRevocationCheck));
                }
                else if (SigningUtility.IsCertificateValid(certificate, additionalCertificates, out chain, allowUntrustedRoot: true, checkRevocationMode: X509RevocationMode.Online))
                {
                    issues.Add(SignatureLog.UntrustedRootWarning(Strings.WarningUntrustedRoot));
                }
                else if (SigningUtility.IsCertificateValid(certificate, additionalCertificates, out chain, allowUntrustedRoot: true, checkRevocationMode: X509RevocationMode.Offline))
                {
                    issues.Add(SignatureLog.UntrustedRootWarning(Strings.WarningOfflineRevocationCheck));
                    issues.Add(SignatureLog.UntrustedRootWarning(Strings.WarningUntrustedRoot));
                }
                // TODO: Make sure you can check untrusted root with revoked certs
                else if (SigningUtility.IsCertificateValid(certificate, additionalCertificates, out chain, allowUntrustedRoot: true, checkRevocationMode: X509RevocationMode.NoCheck))
                {
                    status = SignatureVerificationStatus.Revoked;
                }

                if (chain != null)
                {
                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain)));
                }

                return status;
            }
            catch (SignatureException e)
            {
                issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedWithException, nameof(SigningUtility.IsCertificateValid), e.Message)));
            }

            issues.Add(SignatureLog.InvalidPackageError(Strings.ErrorInvalidCertificateChain));
            return SignatureVerificationStatus.Invalid;
        }
#else
        private PackageVerificationResult VerifySignature(ISignedPackageReader package, Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
