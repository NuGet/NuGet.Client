// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        /// Get all signatures used to sign a package.
        /// </summary>
        /// <remarks>Returns an empty list if the package is unsigned.</remarks>
        Task<IReadOnlyList<Signature>> GetSignaturesAsync(CancellationToken token);

        /// <summary>
        /// Read the signed manifest from a package.
        /// </summary>
        Task<PackageContentManifest> GetSignManifestAsync(CancellationToken token);

        /// <summary>
        /// Read and hash all package entries.
        /// </summary>
        Task<IReadOnlyList<PackageContentManifestFileEntry>> GetContentManifestEntriesAsync(HashAlgorithmName hashAlgorithm, CancellationToken token);

        /// <summary>
        /// Check if a package contains signing information.
        /// </summary>
        /// <returns>True if the package is signed.</returns>
        Task<bool> IsSignedAsync(CancellationToken token);
    }
}
