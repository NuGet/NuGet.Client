// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public interface ISignedPackageVerifier
    {
        Task<VerifySignaturesResult> VerifySignaturesAsync(ISignedPackageReader package, ILogger logger, CancellationToken token);
    }
}