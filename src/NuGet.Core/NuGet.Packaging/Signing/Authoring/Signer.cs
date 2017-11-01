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

        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(SignPackageRequest request, ILogger logger, CancellationToken token)
        {
            // Verify hash is allowed

            // Generate manifest
            var packageEntries = await _package.GetContentManifestEntriesAsync(request.SignatureHashAlgorithm, token);

            var manifest = new PackageContentManifest(
                PackageContentManifest.DefaultVersion,
                request.SignatureHashAlgorithm,
                packageEntries);

            // Generate manifest hash and add file
            var manifestHash = await AddManifestAndGetHashAsync(manifest, request.SignatureHashAlgorithm, token);

            // Create signature
            var signature = await _signatureProvider.CreateSignatureAsync(request, manifestHash, logger, token);

            using (var stream = new MemoryStream(signature.GetBytes()))
            {
                await _package.AddAsync(_specifications.SignaturePath1, stream, token);
            }
        }

        private async Task<SignatureManifest> AddManifestAndGetHashAsync(PackageContentManifest manifest, Common.HashAlgorithmName hashAlgorithmName, CancellationToken token)
        {
            using (var manifestStream = new MemoryStream())
            {
                manifest.Save(manifestStream);
                manifestStream.Position = 0;

                var hash = hashAlgorithmName.GetHashProvider().ComputeHashAsBase64(manifestStream, leaveStreamOpen: true);
                manifestStream.Position = 0;

                await _package.AddAsync(_specifications.ManifestPath, manifestStream, token);

                return new SignatureManifest(SignatureManifest.DefaultVersion, hashAlgorithmName, hash);
            }
        }

        /// <summary>
        /// Remove all signatures from a package.
        /// </summary>
        public async Task RemoveSignaturesAsync(ILogger logger, CancellationToken token)
        {
            foreach (var file in _specifications.AllowedPaths)
            {
                await _package.RemoveAsync(file, token);
            }
        }

        /// <summary>
        /// Remove a single signature from a package.
        /// </summary>
        public Task RemoveSignatureAsync(Signature signature, ILogger logger, CancellationToken token)
        {
            // TODO: counter signing support/removal of counter sign only
            return RemoveSignaturesAsync(logger, token);
        }
    }
}
