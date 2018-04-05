// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class AllowListVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            return Task.FromResult(VerifyAllowList(package, signature, settings));
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            var issues = new List<SignatureLog>();
            var status = SignatureVerificationStatus.Valid;
            var treatIssuesAsErrors = !settings.AllowUntrusted;

            var clientAllowListStatus = VerifyAllowList(
                signature,
                issues,
                settings.ClientCertificateList,
                !settings.AllowNoClientCertificateList,
                treatIssuesAsErrors,
                Strings.Error_NoClientAllowList,
                Strings.Error_NoMatchingClientCertificate);

            var repoAllowListStatus = VerifyAllowList(
                signature,
                issues,
                settings.RepositoryCertificateList,
                !settings.AllowNoRepositoryCertificateList,
                treatIssuesAsErrors,
                Strings.Error_NoRepoAllowList,
                Strings.Error_NoMatchingRepositoryCertificate);

            if (clientAllowListStatus != SignatureVerificationStatus.Valid ||
                repoAllowListStatus != SignatureVerificationStatus.Valid)
            {
                status = SignatureVerificationStatus.Untrusted;
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationStatus VerifyAllowList(
            PrimarySignature signature,
            List<SignatureLog> issues,
            IReadOnlyList<VerificationAllowListEntry> allowList,
            bool requireAllowList,
            bool treatIssuesAsErrors,
            string noListErrorMessage,
            string noMatchErrorMessage)
        {
            var status = SignatureVerificationStatus.Valid;

            if (allowList == null || allowList.Count == 0)
            {
                if (requireAllowList)
                {
                    status = SignatureVerificationStatus.Untrusted;
                    issues.Add(SignatureLog.Issue(fatal: treatIssuesAsErrors, code: NuGetLogCode.NU3034, message: noListErrorMessage));
                }
            }
            else if (!IsSignatureAllowed(signature, allowList))
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.Issue(fatal: treatIssuesAsErrors, code: NuGetLogCode.NU3034, message: noMatchErrorMessage));
            }

            return status;
        }

        private bool IsSignatureAllowed(
            PrimarySignature signature,
            IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            var target = VerificationTarget.Primary;

            if (signature.Type == SignatureType.Repository)
            {
                target = VerificationTarget.Repository;
            }

            foreach (var allowedEntry in allowList)
            {
                // Verify the certificate hash allow list objects
                var certificateHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certificateHashEntry != null)
                {
                    // Get information needed for allow list verification
                    var primarySignatureCertificateFingerprint = CertificateUtility.GetHash(signature.SignerInfo.Certificate, certificateHashEntry.FingerprintAlgorithm);
                    var primarySignatureCertificateFingerprintString = BitConverter.ToString(primarySignatureCertificateFingerprint).Replace("-", "");

                    if (certificateHashEntry.VerificationTarget.HasFlag(target) &&
                        StringComparer.OrdinalIgnoreCase.Equals(certificateHashEntry.Fingerprint, primarySignatureCertificateFingerprintString))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#else
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
