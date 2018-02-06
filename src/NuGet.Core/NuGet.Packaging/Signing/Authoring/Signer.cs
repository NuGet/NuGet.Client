// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            token.ThrowIfCancellationRequested();

            if (await _package.IsZip64Async(token))
            {
                throw new SignatureException(NuGetLogCode.NU3006, Strings.ErrorZip64NotSupported);
            }

            SigningUtility.Verify(request, logger);

            var zipArchiveHash = await _package.GetArchiveHashAsync(request.SignatureHashAlgorithm, token);
            var signatureContent = GenerateSignatureContent(request.SignatureHashAlgorithm, zipArchiveHash);
            var signature = await _signatureProvider.CreateSignatureAsync(request, signatureContent, logger, token);

            using (var stream = new MemoryStream(signature.GetBytes()))
            {
                await _package.AddSignatureAsync(stream, token);
            }
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

        private SignatureContent GenerateSignatureContent(HashAlgorithmName hashAlgorithmName, byte[] zipArchiveHash)
        {
            var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);

            return new SignatureContent(_specifications, hashAlgorithmName, base64ZipArchiveHash);
        }

#else
        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public Task SignAsync(SignPackageRequest request, ILogger logger, CancellationToken token) => throw new NotImplementedException();

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public Task RemoveSignaturesAsync(ILogger logger, CancellationToken token) => throw new NotImplementedException();
#endif
    }
}