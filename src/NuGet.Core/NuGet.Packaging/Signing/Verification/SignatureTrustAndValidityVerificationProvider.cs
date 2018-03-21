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
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = VerifySignatureAndCounterSignature(signature, settings);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifySignatureAndCounterSignature(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            var issues = new List<SignatureLog>();
            var certificateExtraStore = signature.SignedCms.Certificates;

            var primarySignatureVerificationSummary = VerifyValidityAndTrust(signature, settings, certificateExtraStore, issues);
            var status = primarySignatureVerificationSummary.Status;

            if (primarySignatureVerificationSummary.SignatureType == SignatureType.Repository &&
                primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.SelfIssuedCertificate) &&
                !IsSignaturePermittedToBeSelfIssued(signature))
            {
                return new SignedPackageVerificationResult(SignatureVerificationStatus.Illegal, signature, issues);
            }

            if (ShouldFallbackToRepositoryCountersignature(primarySignatureVerificationSummary))
            {
                var counterSignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
                if (counterSignature != null)
                {
                    var countersignatureVerificationSummary = VerifyValidityAndTrust(counterSignature, settings, certificateExtraStore, issues);
                    status = countersignatureVerificationSummary.Status;

                    if (primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.SelfIssuedCertificate) &&
                        !IsSignaturePermittedToBeSelfIssued(counterSignature))
                    {
                        status = SignatureVerificationStatus.Illegal;
                    }
                    else if (primarySignatureVerificationSummary.Flags.HasFlag(SignatureVerificationStatusFlags.CertificateExpired))
                    {
                        if (!Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(signature.SignerInfo.Certificate, countersignatureVerificationSummary.Timestamp))
                        {
                            status = SignatureVerificationStatus.Illegal;
                        }
                    }
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

        private bool IsSignaturePermittedToBeSelfIssued(Signature signature)
        {
            return true;
        }

#else
        private PackageVerificationResult VerifySignatureAndCounterSignature(
            PrimarySignature signature,
            SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}