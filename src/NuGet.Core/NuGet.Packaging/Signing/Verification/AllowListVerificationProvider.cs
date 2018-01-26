// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class AllowListVerificationProvider : ISignatureVerificationProvider
    {
        private HashAlgorithmName _fingerprintAlgorithm;
        private IReadOnlyList<VerificationAllowListEntry> _allowList;

        public AllowListVerificationProvider(IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
            _allowList = allowList;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            return Task.FromResult(VerifyAllowList(package, signature, settings));
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            var status = SignatureVerificationStatus.Trusted;
            var issues = new List<SignatureLog>();

            if (_allowList.Count() > 0 && !IsSignatureAllowed(signature))
            {
                status = SignatureVerificationStatus.Invalid;
                issues.Add(SignatureLog.Issue(fatal: true, code: NuGetLogCode.NU3003, message: string.Format(CultureInfo.CurrentCulture, Strings.Error_NoMatchingCertificate, _fingerprintAlgorithm.ToString())));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private bool IsSignatureAllowed(Signature signature)
        {
            // Get information needed for allow list verification
            var primarySignatureCertificateFingerprint = CertificateUtility.GetHash(signature.SignerInfo.Certificate, _fingerprintAlgorithm);
            var primarySignatureCertificateFingerprintString = BitConverter.ToString(primarySignatureCertificateFingerprint).Replace("-", "");

            foreach (var allowedEntry in _allowList)
            {
                // Verify the certificate hash allow list objects
                var certificateHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certificateHashEntry != null)
                {
                    if (certificateHashEntry.VerificationTarget.HasFlag(VerificationTarget.Primary) &&
                        StringComparer.OrdinalIgnoreCase.Equals(certificateHashEntry.CertificateFingerprint, primarySignatureCertificateFingerprintString))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#else
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
