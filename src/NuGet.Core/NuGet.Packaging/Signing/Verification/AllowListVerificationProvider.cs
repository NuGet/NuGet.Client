// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class AllowListVerificationProvider : ISignatureVerificationProvider
    {
        private IReadOnlyList<VerificationAllowListEntry> _allowList;

        public AllowListVerificationProvider(IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            _allowList = allowList;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            return Task.FromResult(VerifyAllowList(package, signature, settings));
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            var status = SignatureVerificationStatus.Valid;
            var issues = new List<SignatureLog>();

            if (_allowList.Count() > 0 && !IsSignatureAllowed(signature))
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.Issue(fatal: !settings.AllowUntrusted, code: NuGetLogCode.NU3003, message: Strings.Error_NoAllowedCertificate));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private bool IsSignatureAllowed(PrimarySignature signature)
        {
            // To avoid getting the countersignature more than once lets memoize it
            var repositoryCounterSignature = new Lazy<RepositoryCountersignature>(() => RepositoryCountersignature.GetRepositoryCountersignature(signature));

            // Also memoize certificate fingerprints for primary signature and repository countersignature
            var signatureCertFingerprints = new Dictionary<HashAlgorithmName, string>();
            var countersignatureCertFingerprints = new Dictionary<HashAlgorithmName, string>();

            foreach (var allowedEntry in _allowList)
            {
                // Verify the certificate hash allow list objects
                var certHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certHashEntry != null)
                {
                    if (certHashEntry.Placement.HasFlag(SignaturePlacement.PrimarySignature))
                    {
                        if (!signatureCertFingerprints.TryGetValue(certHashEntry.FingerprintAlgorithm, out var signatureCertFingerprint))
                        {
                            signatureCertFingerprint = GetCertFingerprint(signature.SignerInfo.Certificate, certHashEntry.FingerprintAlgorithm);
                            signatureCertFingerprints.Add(certHashEntry.FingerprintAlgorithm, signatureCertFingerprint);
                        }

                        if (IsSignatureTargeted(certHashEntry.VerificationTarget, signature) &&
                            StringComparer.OrdinalIgnoreCase.Equals(certHashEntry.CertificateFingerprint, signatureCertFingerprint))
                        {
                            return true;
                        }
                    }

                    if (certHashEntry.Placement.HasFlag(SignaturePlacement.Countersignature))
                    {
                        // Check if a countersignature exists here to not even get the countersignature if there is no intention to verify it
                        if (repositoryCounterSignature.Value != null)
                        {
                            if (!countersignatureCertFingerprints.TryGetValue(certHashEntry.FingerprintAlgorithm, out var countersignatureCertFingerprint))
                            {
                                countersignatureCertFingerprint = GetCertFingerprint(repositoryCounterSignature.Value.SignerInfo.Certificate, certHashEntry.FingerprintAlgorithm);
                                countersignatureCertFingerprints.Add(certHashEntry.FingerprintAlgorithm, countersignatureCertFingerprint);
                            }

                            if (IsSignatureTargeted(certHashEntry.VerificationTarget, repositoryCounterSignature.Value) &&
                                StringComparer.OrdinalIgnoreCase.Equals(certHashEntry.CertificateFingerprint, countersignatureCertFingerprint))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsSignatureTargeted(VerificationTarget target, Signature signature)
        {
            return (target.HasFlag(VerificationTarget.Author) && signature is AuthorPrimarySignature) ||
                (target.HasFlag(VerificationTarget.Repository) && signature is RepositoryPrimarySignature) ||
                (target.HasFlag(VerificationTarget.Repository) && signature is RepositoryCountersignature);
        }

        private static string GetCertFingerprint(X509Certificate2 cert, HashAlgorithmName algorithm)
        {
            var countersignatureCertFingerprintHash = CertificateUtility.GetHash(cert, algorithm);
            return BitConverter.ToString(countersignatureCertFingerprintHash).Replace("-", "");
        }

#else
        private PackageVerificationResult VerifyAllowList(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
