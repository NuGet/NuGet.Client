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
        private IEnumerable<CertificateHashAllowListEntry> _allowedCertificates;
        private IEnumerable<TrustedSourceAllowListEntry> _allowedSources;
        private IEnumerable<TrustedAuthorAllowListEntry> _allowedAuthors;

        private Dictionary<HashAlgorithmName, string> _primarySignatureCertificateFingerprints;
        private Dictionary<HashAlgorithmName, string> _countersignatureCertificateFingerprints;
        private Lazy<RepositoryCountersignature> _repositoryCounterSignature;

        public AllowListVerificationProvider(IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            _allowedCertificates = allowList.Select(entry => entry as CertificateHashAllowListEntry).Where(entry => entry != null);
            _allowedSources = allowList.Select(entry => entry as TrustedSourceAllowListEntry).Where(entry => entry != null);
            _allowedAuthors = allowList.Select(entry => entry as TrustedAuthorAllowListEntry).Where(entry => entry != null);
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

            // Memoize repository countersignature and certificate fingerprints for primary signature and repository countersignature
            _primarySignatureCertificateFingerprints = new Dictionary<HashAlgorithmName, string>();
            _countersignatureCertificateFingerprints = new Dictionary<HashAlgorithmName, string>();
            _repositoryCounterSignature = new Lazy<RepositoryCountersignature>(() => RepositoryCountersignature.GetRepositoryCountersignature(signature));

            if (!IsSignatureAllowed(signature, settings))
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.Issue(fatal: !settings.AllowUntrusted, code: NuGetLogCode.NU3003, message: Strings.Error_NoAllowedCertificate));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private bool IsSignatureAllowed(PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            if (IsCertificateDisallowed(signature))
            {
                return false;
            }
            if (IsAuthorDisallowed(signature, !settings.AllowNoTrustedAuthors) && IsSourceDisallowed(signature, !settings.AllowNoTrustedSources))
            {
                return false;
            }

            return true;
        }

        private bool IsCertificateDisallowed(PrimarySignature signature)
        {
            // We shouldn't explicitely disallow anything if we don't have an allowList
            if (_allowedCertificates.Count() == 0)
            {
                return false;
            }

            foreach (var allowedEntry in _allowedCertificates)
            {
                if (MatchFingerprint(allowedEntry, signature, allowedEntry.FingerprintAlgorithm, allowedEntry.CertificateFingerprint))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsAuthorDisallowed(PrimarySignature signature, bool requireAllowedAuthorsList)
        {
            // We shouldn't explicitely disallow anything if we don't have an allowList and it is permitted
            if (_allowedAuthors.Count() == 0 && !requireAllowedAuthorsList)
            {
                return false;
            }

            foreach(var author in _allowedAuthors)
            {
                // TODO: Check if the author is allowed
            }

            return true;
        }

        private bool IsSourceDisallowed(PrimarySignature signature, bool requireAllowedSourcesList)
        {
            // We shouldn't explicitely disallow anything if we don't have an allowList and it is permitted
            if (_allowedSources.Count() == 0 && !requireAllowedSourcesList)
            {
                return false;
            }

            foreach (var allowedEntry in _allowedSources)
            {
                foreach(var cert in allowedEntry.Source.Certificates)
                {
                    if (MatchFingerprint(allowedEntry, signature, cert.FingerprintAlgorithm, cert.Fingerprint))
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// Checks placements required by the trusted entry to compare provided fingerprint 
        /// </summary>
        /// <returns>true if any of the placements in the trusted entry match the provided fingerprint</returns>
        private bool MatchFingerprint(VerificationAllowListEntry trustEntry, PrimarySignature signature, HashAlgorithmName fingerprintAlgorithm, string fingerprint)
        {
            if (trustEntry.Placement.HasFlag(SignaturePlacement.PrimarySignature))
            {
                if (!_primarySignatureCertificateFingerprints.TryGetValue(fingerprintAlgorithm, out var primarySignatureCertFingerprint))
                {
                    primarySignatureCertFingerprint = GetCertFingerprint(signature.SignerInfo.Certificate, fingerprintAlgorithm);
                    _primarySignatureCertificateFingerprints.Add(fingerprintAlgorithm, primarySignatureCertFingerprint);
                }

                if (IsSignatureTargeted(trustEntry.VerificationTarget, signature) &&
                    StringComparer.OrdinalIgnoreCase.Equals(fingerprint, primarySignatureCertFingerprint))
                {
                    return true;
                }
            }

            if (trustEntry.Placement.HasFlag(SignaturePlacement.Countersignature))
            {
                // Check if a countersignature exists here to not even get the countersignature if there is no intention to verify it
                if (_repositoryCounterSignature.Value != null)
                {
                    if (!_countersignatureCertificateFingerprints.TryGetValue(fingerprintAlgorithm, out var countersignatureCertFingerprint))
                    {
                        countersignatureCertFingerprint = GetCertFingerprint(_repositoryCounterSignature.Value.SignerInfo.Certificate, fingerprintAlgorithm);
                        _countersignatureCertificateFingerprints.Add(fingerprintAlgorithm, countersignatureCertFingerprint);
                    }

                    if (IsSignatureTargeted(trustEntry.VerificationTarget, _repositoryCounterSignature.Value) &&
                        StringComparer.OrdinalIgnoreCase.Equals(fingerprint, countersignatureCertFingerprint))
                    {
                        return true;
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
