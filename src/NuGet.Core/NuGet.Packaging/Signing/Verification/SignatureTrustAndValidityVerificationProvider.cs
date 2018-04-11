// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureTrustAndValidityVerificationProvider : ISignatureVerificationProvider
    {
        private HashAlgorithmName _fingerprintAlgorithm;

        private SigningSpecifications _specification => SigningSpecifications.V1;

        public SignatureTrustAndValidityVerificationProvider()
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = VerifySignatureAndCountersignature(signature, settings);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifySignatureAndCountersignature(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            var issues = new List<SignatureLog>();
            var certificateExtraStore = signature.SignedCms.Certificates;
            var primarySignatureHasCountersignature = SignatureUtility.HasRepositoryCountersignature(signature);
            var status = SignatureVerificationStatus.Illegal;

            // Only accept untrusted root if the signature has a countersignature that we can validate against
            var verifySettings = new SignatureVerifySettings(
                treatIssuesAsErrors: !settings.AllowIllegal,
                allowUntrustedRoot: primarySignatureHasCountersignature,
                allowUnknownRevocation: settings.AllowUnknownRevocation,
                logOnSignatureExpired: !primarySignatureHasCountersignature);

            var primarySummary = VerifyValidityAndTrust(signature, settings, verifySettings, certificateExtraStore, issues);
            if (primarySummary != null)
            {
                status = primarySummary.Status;

                if (primarySignatureHasCountersignature)
                {
                    if (settings.AlwaysVerifyCountersignature || ShouldFallbackToRepositoryCountersignature(primarySummary))
                    {
                        var countersignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
                        verifySettings = new SignatureVerifySettings(
                            treatIssuesAsErrors: !settings.AllowIllegal,
                            allowUntrustedRoot: false,
                            allowUnknownRevocation: settings.AllowUnknownRevocation,
                            logOnSignatureExpired: true);

                        var counterSummary = VerifyValidityAndTrust(countersignature, settings, verifySettings, certificateExtraStore, issues);
                        status = counterSummary.Status;

                        if (!Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signature.SignerInfo.Certificate, counterSummary.Timestamp))
                        {
                            issues.Add(SignatureLog.Issue(!settings.AllowIllegal, NuGetLogCode.NU3011, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_SignatureNotTimeValid, signature.FriendlyName)));
                            status = SignatureVerificationStatus.Illegal;
                        }
                    }
                    else if (primarySummary.Flags.HasFlag(SignatureVerificationStatusFlags.CertificateExpired))
                    {
                        // We are not adding this log if the primary signature has a countersignature to check the expiration against the countersignature's timestamp.
                        // If the countersignature shouldn't be check and the primary signature was expired, add this log.
                        issues.Add(SignatureLog.Issue(!settings.AllowIllegal, NuGetLogCode.NU3011, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_SignatureNotTimeValid, signature.FriendlyName)));
                    }
                }
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationSummary VerifyValidityAndTrust(
            Signature signature,
            SignedPackageVerifierSettings verifierSettings,
            SignatureVerifySettings settings,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            var timestampIssues = new List<SignatureLog>();

            if (!signature.TryGetValidTimestamp(verifierSettings, _fingerprintAlgorithm, timestampIssues, out var verificationFlags, out var validTimestamp) && !verifierSettings.AllowIgnoreTimestamp)
            {
                issues.AddRange(timestampIssues);

                return null;
            }

            var status = signature.Verify(
                validTimestamp,
                settings,
                _fingerprintAlgorithm,
                certificateExtraStore,
                issues);

            issues.AddRange(timestampIssues);

            return status;
        }

        private bool ShouldFallbackToRepositoryCountersignature(SignatureVerificationSummary primarySummary)
        {
            return primarySummary.SignatureType == SignatureType.Author &&
                ((primarySummary.Status == SignatureVerificationStatus.Illegal &&
                primarySummary.Flags == SignatureVerificationStatusFlags.CertificateExpired) ||
                (primarySummary.Status == SignatureVerificationStatus.Valid &&
                primarySummary.Flags.HasFlag(SignatureVerificationStatusFlags.UntrustedRoot)));
        }

#else
        private PackageVerificationResult VerifySignatureAndCountersignature(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}