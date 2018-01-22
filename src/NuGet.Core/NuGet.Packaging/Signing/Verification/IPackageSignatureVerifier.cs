// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public interface IPackageSignatureVerifier
    {
        Task<VerifySignaturesResult> VerifySignaturesAsync(ISignedPackageReader package, CancellationToken token, Guid parentId);
    }
}