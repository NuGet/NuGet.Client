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

using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Remove or add signature package metadata.
    /// </summary>
    public sealed class Signer
    {
        private readonly ISignedPackage _package;
        private readonly SigningSpecificationsV1 _specifications = SigningSpecifications.V1;
        private readonly ISignatureProvider _signatureProvider;

        /// <summary>
        /// Creates a signer for a specific package.
        /// </summary>
        /// <param name="package">Package to sign or modify.</param>
        public Signer(ISignedPackage package, ISignatureProvider signatureProvider)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
        }


#if IS_DESKTOP
        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(SignPackageRequest request, ILogger logger, CancellationToken token)
        {
            // TODO Verify hash is allowed
            if(request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var packageSignatureProvider = new PackageSignatureProvider(_signatureProvider, request, logger);
            var hashAlgorithm = request.HashAlgorithm.GetHashProvider();
            await _package.AddSignatureAsync(packageSignatureProvider, hashAlgorithm, token);
        }

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public async Task RemoveSignaturesAsync(ILogger logger, CancellationToken token)
        {
            if (await _package.IsSignedAsync(token))
            {
                await _package.RemoveSignatureAsync(token);
            }
        }
#else
        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(SignPackageRequest request, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public async Task RemoveSignaturesAsync(ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
