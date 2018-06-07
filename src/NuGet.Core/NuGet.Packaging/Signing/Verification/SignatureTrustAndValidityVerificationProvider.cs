// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureTrustAndValidityVerificationProvider : ISignatureVerificationProvider
    {
        private readonly HashAlgorithmName _fingerprintAlgorithm;

        public SignatureTrustAndValidityVerificationProvider()
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(
            ISignedPackageReader package,
            PrimarySignature signature,
            SignedPackageVerifierSettings settings,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var result = Verify(signature, settings);

            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult Verify(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            var certificateExtraStore = signature.SignedCms.Certificates;
            var repositoryCountersignatureExists = SignatureUtility.HasRepositoryCountersignature(signature);
            var isRepositoryCountersignatureVerificationRequested = settings.VerificationTarget.HasFlag(VerificationTarget.Repository) &&
                settings.SignaturePlacement.HasFlag(SignaturePlacement.Countersignature);
            var allowDeferralToRepositoryCountersignature = isRepositoryCountersignatureVerificationRequested &&
                repositoryCountersignatureExists;
            var status = SignatureVerificationStatus.Unknown;
            var issues = Enumerable.Empty<SignatureLog>();

            // Only accept untrusted root if the signature has a countersignature that we can validate against
            var verifySettings = new SignatureVerifySettings(
                allowIllegal: settings.AllowIllegal,
                allowUntrusted: settings.AllowUntrusted,
                allowUnknownRevocation: settings.AllowUnknownRevocation,
                reportUnknownRevocation: settings.ReportUnknownRevocation);

            SignatureVerificationSummary primarySummary = null;

            if (settings.SignaturePlacement.HasFlag(SignaturePlacement.PrimarySignature) &&
                VerificationUtility.IsVerificationTarget(signature.Type, settings.VerificationTarget))
            {
                primarySummary = VerifyValidityAndTrust(signature, settings, verifySettings, certificateExtraStore);

                issues = issues.Concat(primarySummary.Issues);

                status = primarySummary.Status;
            }

            Debug.Assert(isRepositoryCountersignatureVerificationRequested != (settings.RepositoryCountersignatureVerificationBehavior == SignatureVerificationBehavior.Never));

            bool shouldVerifyRepositoryCountersignature;

            switch (settings.RepositoryCountersignatureVerificationBehavior)
            {
                case SignatureVerificationBehavior.IfExists:
                    shouldVerifyRepositoryCountersignature = isRepositoryCountersignatureVerificationRequested &&
                        repositoryCountersignatureExists;
                    break;

                case SignatureVerificationBehavior.IfExistsAndIsNecessary:
                    shouldVerifyRepositoryCountersignature = isRepositoryCountersignatureVerificationRequested &&
                        repositoryCountersignatureExists &&
                        (primarySummary == null ||
                            (primarySummary != null &&
                            (HasUntrustedRoot(primarySummary) || IsSignatureExpired(primarySummary))));
                    break;

                case SignatureVerificationBehavior.Always:
                    shouldVerifyRepositoryCountersignature = isRepositoryCountersignatureVerificationRequested;
                    break;

                case SignatureVerificationBehavior.Never:
                    shouldVerifyRepositoryCountersignature = false;
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (shouldVerifyRepositoryCountersignature)
            {
                var countersignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);

                if (countersignature == null)
                {
                    if (settings.RepositoryCountersignatureVerificationBehavior == SignatureVerificationBehavior.Always)
                    {
                        status = SignatureVerificationStatus.Disallowed;
                    }
                }
                else
                {
                    verifySettings = new SignatureVerifySettings(
                        allowIllegal: settings.AllowIllegal,
                        allowUntrusted: settings.AllowUntrusted,
                        allowUnknownRevocation: settings.AllowUnknownRevocation,
                        reportUnknownRevocation: settings.ReportUnknownRevocation);

                    var countersignatureSummary = VerifyValidityAndTrust(countersignature, settings, verifySettings, certificateExtraStore);

                    if (primarySummary == null)
                    {
                        status = countersignatureSummary.Status;
                    }
                    else if (IsSignatureExpired(primarySummary))
                    {
                        if (countersignatureSummary.Status == SignatureVerificationStatus.Valid &&
                            countersignatureSummary.Timestamp != null &&
                            Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signature.SignerInfo.Certificate, countersignatureSummary.Timestamp))
                        {
                            // Exclude the issue of the primary signature being expired since the repository countersignature fulfills the role of a trusted timestamp.
                            issues = issues.Where(log => log.Code != NuGetLogCode.NU3037);

                            status = SignatureVerificationStatus.Valid;
                        }
                    }
                    else if (countersignatureSummary.Status == SignatureVerificationStatus.Valid &&
                        HasUntrustedRoot(primarySummary))
                    {
                        // Exclude the issue of the primary signature being untrusted since the repository countersignature fulfills the role of a trust anchor.
                        issues = issues.Where(log => log.Code != NuGetLogCode.NU3018);

                        status = SignatureVerificationStatus.Valid;
                    }
                    else
                    {
                        status = (SignatureVerificationStatus)Math.Min((int)primarySummary.Status, (int)countersignatureSummary.Status);
                    }

                    issues = issues.Concat(countersignatureSummary.Issues);
                }
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationSummary GetTimestamp(
            Signature signature,
            SignedPackageVerifierSettings verifierSettings,
            out Timestamp timestamp)
        {
            var issues = new List<SignatureLog>();
            SignatureVerificationStatus status;
            SignatureVerificationStatusFlags statusFlags;

            var succeeded = signature.TryGetValidTimestamp(verifierSettings, _fingerprintAlgorithm, issues, out statusFlags, out timestamp);

            status = VerificationUtility.GetSignatureVerificationStatus(statusFlags);

            if (!succeeded)
            {
                if (statusFlags == SignatureVerificationStatusFlags.NoValidTimestamp ||
                    statusFlags == SignatureVerificationStatusFlags.MultipleTimestamps)
                {
                    status = SignatureVerificationStatus.Disallowed;
                }
            }

            return new SignatureVerificationSummary(signature.Type, status, statusFlags, issues);
        }

        private SignatureVerificationSummary VerifyValidityAndTrust(
            Signature signature,
            SignedPackageVerifierSettings verifierSettings,
            SignatureVerifySettings settings,
            X509Certificate2Collection certificateExtraStore)
        {
            Timestamp timestamp;
            var timestampSummary = GetTimestamp(signature, verifierSettings, out timestamp);

            if (timestampSummary.Status != SignatureVerificationStatus.Valid && !verifierSettings.AllowIgnoreTimestamp)
            {
                return new SignatureVerificationSummary(
                    signature.Type,
                    SignatureVerificationStatus.Disallowed,
                    SignatureVerificationStatusFlags.NoValidTimestamp,
                    timestampSummary.Issues);
            }

            var status = signature.Verify(
                timestamp,
                settings,
                _fingerprintAlgorithm,
                certificateExtraStore);

            return new SignatureVerificationSummary(
                status.SignatureType,
                status.Status,
                status.Flags,
                status.Timestamp,
                status.ExpirationTime,
                timestampSummary.Issues.Concat(status.Issues));
        }

        private static bool HasUntrustedRoot(SignatureVerificationSummary summary)
        {
            return summary.SignatureType != SignatureType.Repository &&
                summary.Status != SignatureVerificationStatus.Valid &&
                summary.Flags.HasFlag(SignatureVerificationStatusFlags.UntrustedRoot);
        }

        private static bool IsSignatureExpired(SignatureVerificationSummary summary)
        {
            return summary.SignatureType != SignatureType.Repository && summary.ExpirationTime.HasValue;
        }

#else
        private PackageVerificationResult Verify(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}