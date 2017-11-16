// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#if IS_DESKTOP
using Microsoft.ZipSigningUtilities;
#endif

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A writer that only allows editing for the package signature.
    /// </summary>
    public interface ISignedPackageWriter
    {
        Stream ZipWriteStream { get; }

#if IS_DESKTOP
        /// <summary>
        /// Removes a signature if it exists.
        /// </summary>
        /// <param name="token">CancellationToken</param>
        Task RemoveSignatureAsync(CancellationToken token);


        /// <summary>
        /// Adds a signature in the package.
        /// Throws exception if the package is already signed.
        /// </summary>
        /// <param name="packageSignatureProvider">A signature provider that can be used to generate a SignedCms object.</param>
        /// <param name="token">Cancellation token.</param>
        Task AddSignatureAsync(IZipSignatureProvider packageSignatureProvider, HashAlgorithm hashAlgorithm, CancellationToken token);
#endif
    }
}
