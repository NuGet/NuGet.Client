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
        private readonly PrimarySignature _signature;

        public TestSignatureProvider(PrimarySignature signature)
        {
            _signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        public Task<PrimarySignature> CreatePrimarySignatureAsync(SignPackageRequest request, SignatureContent signatureContent, ILogger logger, CancellationToken token)
        {
            return Task.FromResult(_signature);
        }

        public Task<PrimarySignature> CreateRepositoryCountersignatureAsync(RepositorySignPackageRequest request, PrimarySignature primarySignature, ILogger logger, CancellationToken token)
        {
            return Task.FromResult(_signature);
        }
    }
}
