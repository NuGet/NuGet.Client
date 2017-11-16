// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if IS_DESKTOP
using Microsoft.ZipSigningUtilities;
#endif

using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class NuGetIntegrityVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, ILogger logger, CancellationToken token)
        {
            return VerifyPackageIntegrityAsync(package, signature);
        }

#if IS_DESKTOP
        private async Task<PackageVerificationResult> VerifyPackageIntegrityAsync(ISignedPackageReader package, Signature signature)
        {
            var status = SignatureVerificationStatus.Invalid;
            var issues = new List<SignatureLog>();

            // TODO: Verify algorithm is supported
            //if (!SigningSpecifications.V1.AllowedHashAlgorithms.Contains(signature.SignatureManifest.HashAlgorithm))
            //{
            //    issues.Add(SignatureIssue.InvalidPackageError("Hash algorithm not supported."));
            //    return new SignedPackageVerificationResult(status, signature, issues);
            //}

            try
            {
                await package.ValidateIntegrityAsync(signature.SignatureManifest, CancellationToken.None);
                status = SignatureVerificationStatus.Trusted;
            }
            catch (Exception)
            {
                issues.Add(SignatureLog.InvalidPackageError("Package integrity check failed. The package has been tampered."));
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
