// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Remove or add signature package metadata.
    /// </summary>
    public class Signer
    {
        private readonly ISignPackage _package;

        /// <summary>
        /// Creates a signer for a specific package.
        /// </summary>
        /// <param name="package">Package to sign or modify.</param>
        public Signer(ISignPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public Task SignAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public Task RemoveSignaturesAsync(ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove a single signature from a package.
        /// </summary>
        public Task RemoveSignatureAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
