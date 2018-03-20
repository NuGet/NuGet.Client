// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private HashAlgorithmName _fingerprintAlgorithm;

        private SigningSpecifications _specification => SigningSpecifications.V1;

        public SignatureTrustAndValidityVerificationProvider()
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = VerifyValidityAndTrust(signature, settings);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyValidityAndTrust(PrimarySignature signature, SignedPackageVerifierSettings settings)
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
                return new SignedPackageVerificationResult(SignatureVerificationStatus.Illegal, signature, timestampIssues);
            }

            var certificateExtraStore = signature.SignedCms.Certificates;
            var signatureIssues = new List<SignatureLog>();

            var status = signature.Verify(
                validTimestamp,
                settings,
                _fingerprintAlgorithm,
                certificateExtraStore,
                signatureIssues);

            signatureIssues.AddRange(timestampIssues);

            return new SignedPackageVerificationResult(status, signature, signatureIssues);
        }
#else
        private PackageVerificationResult VerifyValidityAndTrust(PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}