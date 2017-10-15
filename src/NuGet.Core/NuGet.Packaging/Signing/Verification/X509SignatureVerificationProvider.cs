// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

#if NET46
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

#if NET46
        private SignatureVerificationResult VerifySignature(Signature signature)
        {
            var status = SignatureVerificationStatus.Invalid;
            var signerInfo = signature.SignerInfoCollection[0];

            var valid = SigningUtility.IsCertificateValid(signerInfo.Certificate, out var chain, allowUntrustedRoot: false);

            if (valid)
            {
                status = SignatureVerificationStatus.Trusted;
            }

            return new SignatureVerificationResult(status, signature, chain);
        }
#else
        private SignatureVerificationResult VerifySignature(Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
