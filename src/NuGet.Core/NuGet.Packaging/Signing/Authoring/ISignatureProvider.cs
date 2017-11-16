// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
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
        /// <param name="certificate">Certificate to be used while signing the package.</param>
        /// <param name="zipArchiveHash">Hash of the package to be inserted into the package signature.</param>
        /// <param name="logger">Logger</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A signature for the package.</returns>
        Task<Signature> CreateSignatureAsync(SignPackageRequest request, SignatureManifest signatureManifest, ILogger logger, CancellationToken token);
    }
}
