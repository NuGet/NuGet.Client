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

            var primarySignatureStatus = VerifyValidityAndTrust(signature, settings, certificateExtraStore, issues);

            var counterSignatureStatus = SignatureVerificationStatus.Trusted;
            var counterSignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
            if (counterSignature != null)
            {
                counterSignatureStatus = VerifyValidityAndTrust(counterSignature, settings, certificateExtraStore, issues);
            }

            return new SignedPackageVerificationResult((SignatureVerificationStatus)Math.Min((int)primarySignatureStatus, (int)counterSignatureStatus), signature, issues);
        }

        private SignatureVerificationStatus VerifyValidityAndTrust(
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
                return SignatureVerificationStatus.Illegal;
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