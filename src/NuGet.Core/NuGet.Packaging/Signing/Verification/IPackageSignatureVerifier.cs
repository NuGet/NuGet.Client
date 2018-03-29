// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Loads trust providers and verifies package signatures.
    /// </summary>
    public interface IPackageSignatureVerifier
    {
        /// <summary>
        /// Verifies package signature.
        /// </summary>
        /// <param name="package">Package to be verified.</param>
        /// <param name="settings">SignedPackageVerifierSettings to be used when verifying the package.</param>
        /// <param name="token">Cancellation Token.</param>
        /// <param name="parentId">Guid of the parent event.</param>
        /// <returns></returns>
        Task<VerifySignaturesResult> VerifySignaturesAsync(ISignedPackageReader package, SignedPackageVerifierSettings settings, CancellationToken token, Guid parentId);
    }
}