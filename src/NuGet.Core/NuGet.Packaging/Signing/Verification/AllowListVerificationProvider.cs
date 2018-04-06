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
                    TreatIssuesAsErros = treatIssuesAsErrors
                },
                new CertificateListVerificationRequest()
                {
                    CertificateList = settings.RepositoryCertificateList,
                    RequireCertificateList = !settings.AllowNoRepositoryCertificateList,
                    NoListErrorMessage = Strings.Error_NoRepoAllowList,
                    NoMatchErrorMessage = Strings.Error_NoMatchingRepositoryCertificate,
                    Signature = signature,
                    TreatIssuesAsErros = treatIssuesAsErrors
                }
            };

            var allowlistResults = await Task.WhenAll(certificateListVertificationRequests.Select(r => VerifyAllowList(r)));

            return new SignedPackageVerificationResult(GetValidity(allowlistResults), signature, GetIssues(allowlistResults));
        }

        private Task<SignedPackageVerificationResult> VerifyAllowList(CertificateListVerificationRequest request)
        {
            var status = SignatureVerificationStatus.Valid;
            var issues = new List<SignatureLog>();

            if (request.CertificateList == null || request.CertificateList.Count == 0)
            {
                if (request.RequireCertificateList)
                {
                    status = SignatureVerificationStatus.Untrusted;
                    issues.Add(SignatureLog.Issue(fatal: request.TreatIssuesAsErros, code: NuGetLogCode.NU3034, message: request.NoListErrorMessage));
                }
            }
            else if (!IsSignatureAllowed(request.Signature, request.CertificateList))
            {
                status = SignatureVerificationStatus.Untrusted;
                issues.Add(SignatureLog.Issue(fatal: request.TreatIssuesAsErros, code: NuGetLogCode.NU3034, message: request.NoMatchErrorMessage));
            }

            return Task.FromResult(new SignedPackageVerificationResult(status, request.Signature, issues));
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

            var primarySignatureCertificateFingerprintLookUp = new Dictionary<HashAlgorithmName, string>();

            foreach (var allowedEntry in allowList)
            {
                // Verify the certificate hash allow list objects
                var certificateHashEntry = allowedEntry as CertificateHashAllowListEntry;
                if (certificateHashEntry != null)
                {
                    // Get information needed for allow list verification
                    var primarySignatureCertificateFingerprint = GetCertificateFingerprint(
                        signature,
                        certificateHashEntry.FingerprintAlgorithm,
                        primarySignatureCertificateFingerprintLookUp);

                    if (certificateHashEntry.VerificationTarget.HasFlag(target) &&
                        StringComparer.OrdinalIgnoreCase.Equals(certificateHashEntry.Fingerprint, primarySignatureCertificateFingerprint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string GetCertificateFingerprint(
            PrimarySignature signature,
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
            var hasItems = verificationResults.Any();
            var valid = verificationResults.All(e => e.Trust == SignatureVerificationStatus.Valid);

            return valid && hasItems ? SignatureVerificationStatus.Valid : SignatureVerificationStatus.Untrusted;
        }

        private static IEnumerable<SignatureLog> GetIssues(IEnumerable<PackageVerificationResult> verificationResults)
        {
            return verificationResults.SelectMany(r => r.Issues);
        }

        private class CertificateListVerificationRequest
        {
            public PrimarySignature Signature { get; set; }

            public IReadOnlyList<VerificationAllowListEntry> CertificateList { get; set; }

            public bool TreatIssuesAsErros { get; set; }

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
