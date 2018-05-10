// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <remarks>
    /// This is used by NuGetGallery to determine repository signature validity.
    /// </remarks>
    public sealed class RepositorySignatureVerifier
    {
        private readonly HashAlgorithmName _fingerprintAlgorithm;
        private readonly SignedPackageVerifierSettings _settings;

        public RepositorySignatureVerifier()
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
            _settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                alwaysVerifyCountersignature: false,
                repoAllowListEntries: null,
                clientAllowListEntries: null);
        }

        public
#if IS_DESKTOP
            async
#endif
            Task<SignatureVerificationStatus> VerifyAsync(
                ISignedPackageReader reader,
                CancellationToken cancellationToken)
        {
#if IS_DESKTOP
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!await reader.IsSignedAsync(cancellationToken))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnVerify);
            }

            var primarySignature = await reader.GetPrimarySignatureAsync(cancellationToken);

            var integrityVerificationProvider = new IntegrityVerificationProvider();

            var result = await integrityVerificationProvider.GetTrustResultAsync(
                reader,
                primarySignature,
                _settings,
                cancellationToken);

            if (result.Trust != SignatureVerificationStatus.Valid)
            {
                return result.Trust;
            }

            if (primarySignature is RepositoryPrimarySignature)
            {
                return VerifyRepositorySignature(primarySignature, primarySignature.SignedCms.Certificates);
            }

            var countersignature = RepositoryCountersignature.GetRepositoryCountersignature(primarySignature);

            if (countersignature == null)
            {
                throw new SignatureException(Strings.NoRepositorySignature);
            }

            return VerifyRepositorySignature(countersignature, primarySignature.SignedCms.Certificates);
#else
            throw new NotImplementedException();
#endif
        }

#if IS_DESKTOP
        private SignatureVerificationStatus VerifyRepositorySignature(
            Signature signature,
            X509Certificate2Collection certificates)
        {
            var settings = new SignatureVerifySettings(
                treatIssuesAsErrors: !_settings.AllowIllegal,
                allowUntrustedRoot: _settings.AllowUntrusted,
                allowUnknownRevocation: _settings.AllowUnknownRevocation,
                logOnSignatureExpired: false);

            var issues = new List<SignatureLog>();
            Timestamp timestamp = null;

            if (!_settings.AllowIgnoreTimestamp &&
                !signature.TryGetValidTimestamp(
                    _settings,
                    _fingerprintAlgorithm,
                    issues,
                    out var verificationFlags,
                    out timestamp))
            {
                return VerificationUtility.GetSignatureVerificationStatus(verificationFlags);
            }

            var summary = signature.Verify(
                timestamp,
                settings,
                _fingerprintAlgorithm,
                certificates,
                issues);

            return summary.Status;
        }
#endif
    }
}