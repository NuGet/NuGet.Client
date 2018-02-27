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
        private readonly SigningSpecificationsV1 _specifications = SigningSpecifications.V1;
        private readonly SignerRequest _request;

        /// <summary>
        /// Creates a signer for a specific package.
        /// </summary>
        /// <param name="package">Package to sign or modify.</param>
        public Signer(SignerRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

#if IS_DESKTOP
        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(ILogger logger, CancellationToken token)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            token.ThrowIfCancellationRequested();

            SigningUtility.Verify(_request.SignRequest, logger);

            var inputPackagePath = _request.PackagePath;
            var tempPackagePath = Path.GetTempFileName();

            using (var packageReadStream = File.OpenRead(inputPackagePath))
            using (var packageWriteStream = File.Open(tempPackagePath, FileMode.OpenOrCreate))
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                var primarySignature = await package.GetPrimarySignatureAsync(token);

                if (_request.Overwrite && primarySignature != null)
                {
                    await package.RemoveSignatureAsync(token);
                    inputPackagePath = tempPackagePath;
                }
            }

            using (var packageReadStream = File.OpenRead(inputPackagePath))
            using (var packageWriteStream = File.Open(_request.OutputPath, FileMode.OpenOrCreate))
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                if (await package.IsZip64Async(token))
                {
                    throw new SignatureException(NuGetLogCode.NU3006, Strings.ErrorZip64NotSupported);
                }

                var hashAlgorithm = _request.SignRequest.SignatureHashAlgorithm;

                var zipArchiveHash = await package.GetArchiveHashAsync(hashAlgorithm, token);
                var signatureContent = GenerateSignatureContent(hashAlgorithm, zipArchiveHash);
                var signature = await _request.SignatureProvider.CreatePrimarySignatureAsync(_request.SignRequest, signatureContent, logger, token);

                using (var stream = new MemoryStream(signature.GetBytes()))
                {
                    await package.AddSignatureAsync(stream, token);
                }
            }

            FileUtility.Delete(tempPackagePath);
        }

        public async Task RemoveSignatureAsync(ILogger logger, CancellationToken token)
        {
            using (var packageReadStream = File.OpenRead(_request.PackagePath))
            using (var packageWriteStream = File.Open(_request.OutputPath, FileMode.OpenOrCreate))
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                await package.RemoveSignatureAsync(token);
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
        public Task SignAsync(ILogger logger, CancellationToken token) => throw new NotImplementedException();

        /// <summary>
        /// Remove the primary signature from a package.
        /// </summary>
        public Task RemoveSignatureAsync(ILogger logger, CancellationToken token) => throw new NotImplementedException();
#endif
    }
}