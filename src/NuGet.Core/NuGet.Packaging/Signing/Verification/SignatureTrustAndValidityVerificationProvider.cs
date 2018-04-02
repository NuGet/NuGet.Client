// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            // TODO: Add list of allowed untrusted root
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

            var primarySignatureVerificationSummary = VerifyValidityAndTrust(signature, settings, certificateExtraStore, issues);
            var status = primarySignatureVerificationSummary.Status;

            if (status == SignatureVerificationStatus.Illegal &&
                primarySignatureVerificationSummary.Flags == SignatureVerificationStatusFlags.UntrustedRoot &&
                IsSignaturePermittedToChainToUntrustedRoot(signature))
            {
                // TODO: Fix, if this is valid there should not be a warning added to the issues, maybe update the settings to allow it in verification?
                status = SignatureVerificationStatus.Valid;
            }

            if (settings.AllowAlwaysVerifyingCountersignature || ShouldFallbackToRepositoryCountersignature(primarySignatureVerificationSummary))
            {
                var counterSignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
                if (counterSignature != null)
                {
                    var countersignatureIssues = new List<SignatureLog>();
                    // TODO: Add a warning saying that we fallback to the countersignature?

                    var countersignatureVerificationSummary = VerifyValidityAndTrust(counterSignature, settings, certificateExtraStore, countersignatureIssues);
                    status = countersignatureVerificationSummary.Status;

                    if (status == SignatureVerificationStatus.Illegal &&
                        countersignatureVerificationSummary.Flags == SignatureVerificationStatusFlags.UntrustedRoot &&
                        IsSignaturePermittedToChainToUntrustedRoot(counterSignature))
                    {
                        // TODO: Fix, if this is valid there should not be a warning added to the issues, maybe update the settings to allow it in verification?
                        status = SignatureVerificationStatus.Valid;
                    }

                    if (primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.CertificateExpired))
                    {
                        if (!Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signature.SignerInfo.Certificate, countersignatureVerificationSummary.Timestamp))
                        {
                            issues.Add(SignatureLog.Issue(!settings.AllowIllegal, NuGetLogCode.NU3011, Strings.SignatureNotTimeValid));
                            status = SignatureVerificationStatus.Illegal;
                        }
                    }

                    issues = countersignatureIssues;
                }
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationSummary VerifyValidityAndTrust(
            Signature signature,
            SignedPackageVerifierSettings settings,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            var timestampIssues = new List<SignatureLog>();

            Timestamp validTimestamp;
            try
            {
                validTimestamp = signature.GetValidTimestamp(
                    settings,
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
                primarySignatureVerificationSummary.Status == SignatureVerificationStatus.Illegal &&
                (primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.CertificateExpired) ||
                primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.GeneralChainBuildingIssues));
        }

        private bool IsSignaturePermittedToChainToUntrustedRoot(Signature signature)
        {
            // TODO: Check if signature is in list of allowd untrusted root
            return true;
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