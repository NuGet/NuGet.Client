// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class TestSignatureProvider : ISignatureProvider
    {
        private readonly Signature _signature;

        public TestSignatureProvider(Signature signature)
        {
            _signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, SignatureContent signatureContent, ILogger logger, CancellationToken token)
        {
            return Task.FromResult(_signature);
        }
    }
}
