// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public class X509SignatureVerificationProvider : ISignatureVerificationProvider
    {
        public Task<SignatureVerificationResult> GetTrustResultAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            var result = VerifySignature(signature);

            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private SignatureVerificationResult VerifySignature(Signature signature)
        {
            var status = SignatureVerificationStatus.Invalid;
            var signerInfo = signature.SignerInfo;
            var signatureIssues = new List<SignatureIssue>();

            var validAndUntrusted = SigningUtility.IsCertificateValid(signerInfo.Certificate, out var chain, allowUntrustedRoot: true);
            var valid = SigningUtility.IsCertificateValid(signerInfo.Certificate, out chain, allowUntrustedRoot: false);

            if (valid)
            {
                status = SignatureVerificationStatus.Trusted;
            }
            else if (validAndUntrusted)
            {
                status = SignatureVerificationStatus.Untrusted;
                signatureIssues.Add(SignatureIssue.UntrustedRootWarning(Strings.ErrorSigningCertUntrustedRoot));
            }
            else {
                status = SignatureVerificationStatus.Invalid;
                signatureIssues.Add(SignatureIssue.InvalidPackageError(Strings.ErrorPackageTampered));
            }

            return new SignatureVerificationResult(status, signature, chain, signatureIssues);
        }
#else
        private SignatureVerificationResult VerifySignature(Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
