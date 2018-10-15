// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A readonly package that can provide signatures and a sign manifest from a package.
    /// </summary>
    public interface ISignedPackageReader : IDisposable
    {
        /// <summary>
        /// Get package signature.
        /// </summary>
        /// <remarks>Returns a null if the package is unsigned.</remarks>
        Task<PrimarySignature> GetPrimarySignatureAsync(CancellationToken token);

        /// <summary>
        /// Check if a package contains signing information.
        /// </summary>
        /// <returns>True if the package is signed.</returns>
        Task<bool> IsSignedAsync(CancellationToken token);

        /// <summary>
        /// Gets the hash of an archive to be embedded in the package signature.
        /// </summary>
        Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithm, CancellationToken token);

        /// <summary>
        /// Checks for the integrity of a package
        /// </summary>
        /// <param name="signatureContent">SignatureContent with expected hash value and hash algorithm used</param>
        /// <returns></returns>
        Task ValidateIntegrityAsync(SignatureContent signatureContent, CancellationToken token);

        /// <summary>
        /// Get the hash of the package content excluding signature context for signed package.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>hash of the unsigned content of the signed package. but return null for unsigned package.</returns>
        string GetContentHashForSignedPackage(CancellationToken token);

        /// <summary>
        /// Indicates if the signature verification should be skipped. This could be because of it being unsupported or
        /// because a package should not be verified.
        /// </summary>
        /// <param name="verifierSettings">Package verification settings. Include information about what is allowed.</param>
        /// <exception cref="SignatureException">if the ISignedPackageReader does not support either verifing or skipping the signature verification.</exception>
        /// <returns>boolean indicating if the verification of the signature should be skipped.</returns>
        bool CanVerifySignedPackages(SignedPackageVerifierSettings verifierSettings);
    }
}
