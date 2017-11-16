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
    public class NuGetSignatureHeaderVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, ILogger logger, CancellationToken token)
        {
            var result = VerifyHeader(signature);

            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyHeader(Signature signature)
        {
            var status = SignatureVerificationStatus.Trusted;
            var issues = new List<SignatureLog>();
            var header = signature.Header;

            if (!SigningSpecifications.V1.SupportedVersions.Contains($"{header.MajorVersion}.{header.MinorVersion}"))
            {
                issues.Add(SignatureLog.InvalidPackageError("Signature header version not supported by Specification V1."));
                status = SignatureVerificationStatus.Invalid;
            }

            if (!header.VerifySignature(SigningSpecifications.V1.NuGetPackageSignatureFormatSignature))
            {
                issues.Add(SignatureLog.InvalidPackageError("Signature header not supported by Specification V1."));
                status = SignatureVerificationStatus.Invalid;
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }
#else
        private PackageVerificationResult VerifyHeader(Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
