// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class AllowListVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            return VerifyAllowListAsync(package, signature, settings);
        }

#if IS_DESKTOP
        private async Task<PackageVerificationResult> VerifyAllowListAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            var treatIssuesAsErrors = !settings.AllowUntrusted;

            var certificateListVertificationRequests = new List<CertificateListVerificationRequest>()
            {
                new CertificateListVerificationRequest()
                {
                    CertificateList = settings.ClientCertificateList,
                    RequireCertificateList = !settings.AllowNoClientCertificateList,
                    NoListErrorMessage = Strings.Error_NoClientAllowList,
                    NoMatchErrorMessage = Strings.Error_NoMatchingClientCertificate,
                    Signature = signature,
                    TreatIssuesAsErrors = treatIssuesAsErrors
                },
                new CertificateListVerificationRequest()
                {
                    CertificateList = settings.RepositoryCertificateList,
                    RequireCertificateList = !settings.AllowNoRepositoryCertificateList,
                    NoListErrorMessage = Strings.Error_NoRepoAllowList,
                    NoMatchErrorMessage = Strings.Error_NoMatchingRepositoryCertificate,
                    Signature = signature,
                    TreatIssuesAsErrors = treatIssuesAsErrors
                }
            };

            var allowListResults = await Task.WhenAll(certificateListVertificationRequests.Select(r => VerifyAllowList(r, settings.AllowUntrusted)));

            return new SignedPackageVerificationResult(GetValidity(allowListResults), signature, allowListResults.SelectMany(r => r.Issues));
        }

        /// <summary>
        /// Verify an allow list with a given request
        /// </summary>
        /// <param name="request">Information about the allow list verification to perform</param>
        /// <remarks>This method should never return a status unknown. Min is used to take the most severe status in <see cref="GetValidity"/>
        /// therefore, if unknown is returned the verification process will return an unknown status for the whole operation</remarks>
        private Task<SignedPackageVerificationResult> VerifyAllowList(CertificateListVerificationRequest request, bool allowUntrusted)
        {
            var status = SignatureVerificationStatus.Valid;
            var issues = new List<SignatureLog>();

            if (request.CertificateList == null || request.CertificateList.Count == 0)
            {
                if (request.RequireCertificateList)
                {
                    status = SignatureVerificationStatus.Disallowed;
                    issues.Add(SignatureLog.Error(code: NuGetLogCode.NU3034, message: request.NoListErrorMessage));
                }
            }
            else if (!IsSignatureAllowed(request.Signature, request.CertificateList))
            {
                if (!allowUntrusted)
                {
                    status = SignatureVerificationStatus.Disallowed;
                }

                issues.Add(SignatureLog.Issue(fatal: request.TreatIssuesAsErrors, code: NuGetLogCode.NU3034, message: request.NoMatchErrorMessage));
            }

            return Task.FromResult(new SignedPackageVerificationResult(status, request.Signature, issues));
        }

        private bool IsSignatureAllowed(
            PrimarySignature signature,
            IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            var primarySignatureCertificateFingerprintLookUp = new Dictionary<HashAlgorithmName, string>();
            var countersignatureCertificateFingerprintLookUp = new Dictionary<HashAlgorithmName, string>();
            var repositoryCountersignature = new Lazy<RepositoryCountersignature>(() => RepositoryCountersignature.GetRepositoryCountersignature(signature));

            foreach (var allowedEntry in allowList)
            {
                // Verify the certificate hash allow list objects
                var certificateHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certificateHashEntry != null)
                {
                    if (certificateHashEntry.Placement.HasFlag(SignaturePlacement.PrimarySignature))
                    {
                        // Get information needed for allow list verification
                        var primarySignatureCertificateFingerprint = GetCertificateFingerprint(
                            signature,
                            certificateHashEntry.FingerprintAlgorithm,
                            primarySignatureCertificateFingerprintLookUp);

                        if (IsSignatureTargeted(certificateHashEntry.Target, signature) &&
                            StringComparer.OrdinalIgnoreCase.Equals(certificateHashEntry.Fingerprint, primarySignatureCertificateFingerprint))
                        {
                            if (ShouldVerifyOwners(certificateHashEntry as TrustedSignerAllowListEntry, signature as IRepositorySignature, out var allowedOwners, out var actualOwners))
                            {
                                return allowedOwners.Intersect(actualOwners).Any();
                            }

                            return true;
                        }
                    }
                    if (certificateHashEntry.Placement.HasFlag(SignaturePlacement.Countersignature))
                    {
                        if (repositoryCountersignature.Value != null)
                        {
                            // Get information needed for allow list verification
                            var countersignatureCertificateFingerprint = GetCertificateFingerprint(
                                repositoryCountersignature.Value,
                                certificateHashEntry.FingerprintAlgorithm,
                                countersignatureCertificateFingerprintLookUp);

                            if (IsSignatureTargeted(certificateHashEntry.Target, repositoryCountersignature.Value) &&
                                StringComparer.OrdinalIgnoreCase.Equals(certificateHashEntry.Fingerprint, countersignatureCertificateFingerprint))
                            {
                                if (ShouldVerifyOwners(certificateHashEntry as TrustedSignerAllowListEntry, repositoryCountersignature.Value as IRepositorySignature, out var allowedOwners, out var actualOwners))
                                {
                                    return allowedOwners.Intersect(actualOwners).Any();
                                }

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool ShouldVerifyOwners(TrustedSignerAllowListEntry entry, IRepositorySignature repoSignature, out IReadOnlyList<string> allowedOwners, out IReadOnlyList<string> actualOwners)
        {
            allowedOwners = null;
            actualOwners = null;

            if (entry != null && entry.Target.HasFlag(VerificationTarget.Repository) && entry.Owners != null && entry.Owners.Any() && repoSignature != null)
            {
                allowedOwners = entry.Owners ?? Enumerable.Empty<string>().ToList();
                actualOwners = repoSignature.PackageOwners ?? Enumerable.Empty<string>().ToList();

                return true;
            }

            return false;
        }

        private static bool IsSignatureTargeted(VerificationTarget target, Signature signature)
        {
            return (target.HasFlag(VerificationTarget.Author) && signature is AuthorPrimarySignature) ||
                (target.HasFlag(VerificationTarget.Repository) && signature is RepositoryPrimarySignature) ||
                (target.HasFlag(VerificationTarget.Repository) && signature is RepositoryCountersignature);
        }

        private static string GetCertificateFingerprint(
            Signature signature,
            HashAlgorithmName fingerprintAlgorithm,
            IDictionary<HashAlgorithmName, string> CertificateFingerprintLookUp)
        {
            if (!CertificateFingerprintLookUp.TryGetValue(fingerprintAlgorithm, out var fingerprintString))
            {
                var primarySignatureCertificateFingerprint = CertificateUtility.GetHash(signature.SignerInfo.Certificate, fingerprintAlgorithm);
                fingerprintString = BitConverter.ToString(primarySignatureCertificateFingerprint).Replace("-", "");
                CertificateFingerprintLookUp[fingerprintAlgorithm] = fingerprintString;
            }
            
            return fingerprintString;
        }

        private static SignatureVerificationStatus GetValidity(IEnumerable<PackageVerificationResult> verificationResults)
        {
            return verificationResults.Min(e => e.Trust);
        }

        private class CertificateListVerificationRequest
        {
            public PrimarySignature Signature { get; set; }

            public IReadOnlyList<VerificationAllowListEntry> CertificateList { get; set; }

            public bool TreatIssuesAsErrors { get; set; }

            public bool RequireCertificateList { get; set; }

            public string NoListErrorMessage { get; set; }

            public string NoMatchErrorMessage { get; set; }
        }

#else
        private Task<PackageVerificationResult> VerifyAllowListAsync(ISignedPackageReader package, PrimarySignature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
