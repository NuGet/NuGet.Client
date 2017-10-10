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
    public class TestTrustProvider : ISignTrustProvider
    {
        public Task<SignatureTrustResult> GetTrustResultAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            var result = new SignatureTrustResult(signature.TestTrust);
            return Task.FromResult(result);
        }
    }
}
