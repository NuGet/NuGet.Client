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
        private IEnumerable<NuGetSignatureAllowListObject> _allowList;
        private bool _shouldCheckAllowList;

        public AllowListVerificationProvider(HashAlgorithmName fingerprintAlgorithm, IEnumerable<NuGetSignatureAllowListObject> allowList)
        {
            _fingerprintAlgorithm = fingerprintAlgorithm;
            _allowList = allowList;
            _shouldCheckAllowList = _allowList != null && _allowList.Count() > 0;
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

            if (_shouldCheckAllowList && !IsSignatureAllowed(signature))
            {
                status = SignatureVerificationStatus.Invalid;
                issues.Add(SignatureLog.Issue(fatal: true, code: NuGetLogCode.NU3003, message: string.Format(CultureInfo.CurrentCulture, Strings.Error_NoMatchingCertificate, _fingerprintAlgorithm.ToString())));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private bool IsSignatureAllowed(Signature signature)
        {
            // Get information needed for allow list verification
            var primarySingatureCertificateFingerprint = CertificateUtility.GetHash(signature.SignerInfo.Certificate, _fingerprintAlgorithm);
            var primarySingatureCertificateFingerprintString = BitConverter.ToString(primarySingatureCertificateFingerprint).Replace("-", "");

            foreach (var allowedObject in _allowList)
            {
                // Verify the certificate hash allow list objects
                if (allowedObject is NuGetSignatureCertificateHashAllowListObject)
                {
                    if (IsTargetingPrimarySignature(allowedObject.VerificationTarget) &&
                        StringComparer.OrdinalIgnoreCase.Equals(((NuGetSignatureCertificateHashAllowListObject)allowedObject).CertificateFingerprint, primarySingatureCertificateFingerprintString))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTargetingPrimarySignature(NuGetSignatureVerificationTarget verificationTarget)
        {
            return (verificationTarget & NuGetSignatureVerificationTarget.Primary) == NuGetSignatureVerificationTarget.Primary;
        }

        private static bool IsTargetingRepositorySignature(NuGetSignatureVerificationTarget verificationTarget)
        {
            return (verificationTarget & NuGetSignatureVerificationTarget.Repository) == NuGetSignatureVerificationTarget.Repository;
        }

#else
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
