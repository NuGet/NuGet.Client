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

            // Only accept untrusted root if the signature has a countersignature that we can validate against
            var verifySettings = new SignatureVerifySettings(
                treatIssuesAsErrors: !settings.AllowIllegal,
                allowUntrustedRoot: primarySignatureHasCountersignature,
                allowUnknownRevocation: settings.AllowUnknownRevocation,
                logOnSignatureExpired: !primarySignatureHasCountersignature);

            var primarySignatureVerificationSummary = VerifyValidityAndTrust(signature, settings, verifySettings, certificateExtraStore, issues);
            var status = primarySignatureVerificationSummary.Status;

            if (primarySignatureHasCountersignature)
            {
                if (settings.AlwaysVerifyCountersignature || ShouldFallbackToRepositoryCountersignature(primarySignatureVerificationSummary))
                {
                    var counterSignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
                    verifySettings = new SignatureVerifySettings(
                        treatIssuesAsErrors: !settings.AllowIllegal,
                        allowUntrustedRoot: false,
                        allowUnknownRevocation: settings.AllowUnknownRevocation,
                        logOnSignatureExpired: true);

                    var countersignatureVerificationSummary = VerifyValidityAndTrust(counterSignature, settings, verifySettings, certificateExtraStore, issues);
                    status = countersignatureVerificationSummary.Status;

                    if (!Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signature.SignerInfo.Certificate, countersignatureVerificationSummary.Timestamp))
                    {
                        issues.Add(SignatureLog.Issue(!settings.AllowIllegal, NuGetLogCode.NU3011, string.Format(CultureInfo.CurrentCulture, Strings.SignatureNotTimeValid, nameof(AuthorPrimarySignature))));
                        status = SignatureVerificationStatus.Illegal;
                    }
                }
                else if (primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.CertificateExpired))
                {
                    // We are not adding this log if the primary signature has a countersignature to check the expiration against the countersignature's timestamp.
                    // If the countersignature shouldn't be check and the primary signature was expired, add this log.
                    issues.Add(SignatureLog.Issue(!settings.AllowIllegal, NuGetLogCode.NU3011, string.Format(CultureInfo.CurrentCulture, Strings.SignatureNotTimeValid, nameof(AuthorPrimarySignature))));
                }
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationSummary VerifyValidityAndTrust(
            Signature signature,
            SignedPackageVerifierSettings timestampSettings,
            SignatureVerifySettings settings,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            var timestampIssues = new List<SignatureLog>();

            Timestamp validTimestamp;
            try
            {
                validTimestamp = signature.GetValidTimestamp(
                    timestampSettings,
                    _fingerprintAlgorithm,
                    timestampIssues);
            }
            catch (TimestampException)
            {
                issues.AddRange(timestampIssues);

                return new SignatureVerificationSummary(signature.Type, SignatureVerificationStatus.Illegal, SignatureVerificationStatusFlags.InvalidTimestamp);
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

        private bool ShouldFallbackToRepositoryCountersignature(SignatureVerificationSummary primarySignatureVerificationSummary)
        {
            return primarySignatureVerificationSummary.SignatureType == SignatureType.Author &&
                ((primarySignatureVerificationSummary.Status == SignatureVerificationStatus.Illegal &&
                primarySignatureVerificationSummary.Flags == SignatureVerificationStatusFlags.CertificateExpired) ||
                (primarySignatureVerificationSummary.Status == SignatureVerificationStatus.Valid &&
                primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.UntrustedRoot)));
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