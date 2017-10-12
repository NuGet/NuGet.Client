// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging.Test.SigningTests
{
    /// <summary>
    /// TEMPORARY trust provider for signing
    /// </summary>
    public class SignatureVerificationProvider : ISignatureVerificationProvider
    {
        public Task<SignatureVerificationResult> GetTrustResultAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            var result = new SignatureVerificationResult(signature.TestTrust);
            return Task.FromResult(result);
        }
    }
}
