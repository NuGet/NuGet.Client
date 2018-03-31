// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Signing
{
    public static class RepositorySignatureInfoUtility
    {
        /// <summary>
        /// Gets SignedPackageVerifierSettings from a given RepositorySignatureInfo. 
        /// </summary>
        /// <param name="repoSignatureInfo">RepositorySignatureInfo to be used.</param>
        /// <param name="commonSignedPackageVerifierSettings">SignedPackageVerifierSettings to be used if RepositorySignatureInfo is unavailable.</param>
        /// <returns>SignedPackageVerifierSettings based on the RepositorySignatureInfo and SignedPackageVerifierSettings.</returns>
        public static SignedPackageVerifierSettings GetSignedPackageVerifierSettings(
            RepositorySignatureInfo repoSignatureInfo,
            SignedPackageVerifierSettings commonSignedPackageVerifierSettings)
        {
            if (commonSignedPackageVerifierSettings == null)
            {
                throw new ArgumentNullException(nameof(commonSignedPackageVerifierSettings));
            }

            if (repoSignatureInfo == null)
            {
                return commonSignedPackageVerifierSettings;
            }
            else
            {
                var repositoryAllowList = GetRepositoryAllowList(repoSignatureInfo.RepositoryCertificateInfos);
                var allowUnsigned = !repoSignatureInfo.AllRepositorySigned;
                var allowUntrusted = repositoryAllowList?.Count > 0 ? false : commonSignedPackageVerifierSettings.AllowUntrusted;

                return new SignedPackageVerifierSettings(
                    allowUnsigned: allowUnsigned,
                    allowIllegal: commonSignedPackageVerifierSettings.AllowIllegal,
                    allowUntrusted: allowUntrusted,
                    allowUntrustedSelfIssuedCertificate: commonSignedPackageVerifierSettings.AllowUntrustedSelfIssuedCertificate,
                    allowIgnoreTimestamp: commonSignedPackageVerifierSettings.AllowIgnoreTimestamp,
                    allowMultipleTimestamps: commonSignedPackageVerifierSettings.AllowMultipleTimestamps,
                    allowNoTimestamp: commonSignedPackageVerifierSettings.AllowNoTimestamp,
                    allowUnknownRevocation: commonSignedPackageVerifierSettings.AllowUnknownRevocation,
                    repoAllowListEntries: repositoryAllowList?.AsReadOnly(),
                    clientAllowListEntries: null);
            }
        }

        private static List<CertificateHashAllowListEntry> GetRepositoryAllowList(IEnumerable<IRepositoryCertificateInfo> repositoryCertificateInfos)
        {
            List<CertificateHashAllowListEntry> repositoryAllowList = null;

            if (repositoryCertificateInfos != null)
            {
                repositoryAllowList = new List<CertificateHashAllowListEntry>();

                foreach (var certInfo in repositoryCertificateInfos)
                {
                    var verificationTarget = VerificationTarget.Repository | VerificationTarget.Primary;

                    AddCertificateFingerprintIntoAllowList(verificationTarget, HashAlgorithmName.SHA256, certInfo, repositoryAllowList);
                    AddCertificateFingerprintIntoAllowList(verificationTarget, HashAlgorithmName.SHA384, certInfo, repositoryAllowList);
                    AddCertificateFingerprintIntoAllowList(verificationTarget, HashAlgorithmName.SHA512, certInfo, repositoryAllowList);
                }
            }

            return repositoryAllowList;
        }

        private static void AddCertificateFingerprintIntoAllowList(
            VerificationTarget target,
            HashAlgorithmName algorithm,
            IRepositoryCertificateInfo certInfo,
            List<CertificateHashAllowListEntry> allowList)
        {
            var fingerprint = certInfo.Fingerprints[algorithm.ConvertToOidString()];

            if (!string.IsNullOrEmpty(fingerprint))
            {
                allowList.Add(new CertificateHashAllowListEntry(target, fingerprint, algorithm));
            }
        }
    }
}
