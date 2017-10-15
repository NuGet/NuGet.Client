// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging.Test.SigningTests
{
    public class SelfSignedSignatureVerificationProvider : ISignatureVerificationProvider
    {
        public Task<SignatureVerificationResult> GetTrustResultAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            var result = new SignatureVerificationResult(signature.TestTrust);
            return Task.FromResult(result);
        }
    }
}
