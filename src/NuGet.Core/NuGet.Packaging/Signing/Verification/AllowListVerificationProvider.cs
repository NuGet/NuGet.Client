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
            var fatalFailures = !settings.AllowUntrusted;
            var clientAllowListStatus = VerifyAllowList(signature, issues, settings.ClientAllowListEntries, fatalFailures, Strings.Error_NoMatchingCertificate_Client);
            var repoAllowListStatus = VerifyAllowList(signature, issues, settings.RepositoryAllowListEntries, fatalFailures, Strings.Error_NoMatchingCertificate_Repo);

            if (clientAllowListStatus != SignatureVerificationStatus.Valid ||
                repoAllowListStatus != SignatureVerificationStatus.Valid)
            {
                status = SignatureVerificationStatus.Untrusted;
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationStatus VerifyAllowList(
            Signature signature,
            List<SignatureLog> issues,
            IReadOnlyList<VerificationAllowListEntry> allowList,
            bool fatalFailures,
            string errorMessage)
        {
            var status = SignatureVerificationStatus.Valid;

            if (allowList?.Count > 0 && !IsSignatureAllowed(signature, allowList))
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.Issue(fatal: fatalFailures, code: NuGetLogCode.NU3003, message: errorMessage));
            }

            return status;
        }

        private bool IsSignatureAllowed(
            Signature signature,
            IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            foreach (var allowedEntry in allowList)
            {
                // Verify the certificate hash allow list objects
                var certificateHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certificateHashEntry != null)
                {
                    // Get information needed for allow list verification
                    var primarySignatureCertificateFingerprint = CertificateUtility.GetHash(signature.SignerInfo.Certificate, certificateHashEntry.FingerprintAlgorithm);
                    var primarySignatureCertificateFingerprintString = BitConverter.ToString(primarySignatureCertificateFingerprint).Replace("-", "");

                    if (certificateHashEntry.VerificationTarget.HasFlag(VerificationTarget.Primary) &&
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
