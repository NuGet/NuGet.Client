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
    public class NuGetIntegrityVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, CancellationToken token)
        {
            return VerifyPackageIntegrityAsync(package, signature);
        }

#if IS_DESKTOP
        private async Task<PackageVerificationResult> VerifyPackageIntegrityAsync(ISignedPackageReader package, Signature signature)
        {
            var status = SignatureVerificationStatus.Invalid;
            var issues = new List<SignatureLog>();

            var validHashOids = SigningSpecifications.V1.AllowedHashAlgorithmOids;
            var signatureHashOid = signature.SignatureManifest.HashAlgorithm.ConvertToOidString();
            if (!validHashOids.Contains(signatureHashOid, StringComparer.InvariantCultureIgnoreCase))
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.SignatureFailureInvalidHashAlgorithmOid));
                issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureDebug_HashOidFound, signatureHashOid)));
                return new SignedPackageVerificationResult(status, signature, issues);
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureHashAlgorithm, signature.SignatureManifest.HashAlgorithm)));

            try
            {
                await package.ValidateIntegrityAsync(signature.SignatureManifest, CancellationToken.None);
                status = SignatureVerificationStatus.Trusted;
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.InvalidPackageError(Strings.SignatureFailurePackageTampered));
                issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedWithException, nameof(VerifyPackageIntegrityAsync), e.Message)));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }
#else
        private Task<PackageVerificationResult> VerifyPackageIntegrityAsync(ISignedPackageReader package, Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
