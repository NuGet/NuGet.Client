// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Creates Signatures that can be added to packages.
    /// </summary>
    public interface ISignatureProvider
    {
        /// <summary>
        /// Create a signature.
        /// </summary>
        /// <param name="request">Request containing the certificate to sign with.</param>
        /// <param name="signatureManifest">Package content manifest hash.</param>
        /// <returns>A signature for the manifest hash.</returns>
        Task<Signature> CreateSignatureAsync(SignPackageRequest request, SignatureManifest signatureManifest, ILogger logger, CancellationToken token);
    }
}
