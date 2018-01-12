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
    public class WhitelistVerificationProvider : ISignatureVerificationProvider
    {
        private HashAlgorithmName _fingerprintAlgorithm;
        private IEnumerable<NuGetSignatureWhitelistObject> _whitelist;
        private bool _shouldCheckWhitelist;

        public WhitelistVerificationProvider(HashAlgorithmName fingerprintAlgorithm, IEnumerable<NuGetSignatureWhitelistObject> whitelist)
        {
            _fingerprintAlgorithm = fingerprintAlgorithm;
            _whitelist = whitelist;
            _shouldCheckWhitelist = _whitelist != null && _whitelist.Count() > 0;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            return Task.FromResult(VerifyWhitelist(package, signature, settings));
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyWhitelist(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            var status = SignatureVerificationStatus.Trusted;
            var issues = new List<SignatureLog>();
            var certificateFingerprint = CertificateUtility.GetCertificateFingerprint(_fingerprintAlgorithm, signature.SignerInfo.Certificate);

            if (_shouldCheckWhitelist && !_whitelist.Where(whitelistObject => StringComparer.Ordinal.Equals(whitelistObject.CertificateFingerprint, certificateFingerprint)).Any())
            {
                status = SignatureVerificationStatus.Invalid;
                issues.Add(SignatureLog.Issue(fatal: true, code: NuGetLogCode.NU3003, message: string.Format(CultureInfo.CurrentCulture, Strings.Error_NoMatchingCertificate, _fingerprintAlgorithm.ToString())));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }
#else
        private PackageVerificationResult VerifyWhitelist(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
