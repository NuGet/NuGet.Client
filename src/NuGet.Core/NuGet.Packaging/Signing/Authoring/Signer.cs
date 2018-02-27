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
        private readonly SignerOptions _options;

        /// <summary>
        /// Creates a signer for a specific package.
        /// </summary>
        /// <param name="options">Signer options to specify how to perform signer actions</param>
        public Signer(SignerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

#if IS_DESKTOP
        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public async Task SignAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            SigningUtility.Verify(_options.SignRequest, _options.Logger);

            var inputPackagePath = _options.PackageFilePath;
            var tempPackagePath = Path.GetTempFileName();

            try
            {
                using (var packageReadStream = File.OpenRead(inputPackagePath))
                using (var packageWriteStream = File.Open(tempPackagePath, FileMode.OpenOrCreate))
                using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
                {
                    var primarySignature = await package.GetPrimarySignatureAsync(token);

                    if (_options.Overwrite && primarySignature != null)
                    {
                        await package.RemoveSignatureAsync(token);
                        inputPackagePath = tempPackagePath;
                    }
                }

                using (var packageReadStream = File.OpenRead(inputPackagePath))
                using (var packageWriteStream = File.Open(_options.OutputFilePath, FileMode.OpenOrCreate))
                using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
                {
                    if (await package.IsZip64Async(token))
                    {
                        throw new SignatureException(NuGetLogCode.NU3006, Strings.ErrorZip64NotSupported);
                    }

                    var hashAlgorithm = _options.SignRequest.SignatureHashAlgorithm;

                    var zipArchiveHash = await package.GetArchiveHashAsync(hashAlgorithm, token);
                    var signatureContent = GenerateSignatureContent(hashAlgorithm, zipArchiveHash);
                    var signature = await _options.SignatureProvider.CreatePrimarySignatureAsync(_options.SignRequest, signatureContent, _options.Logger, token);

                    using (var stream = new MemoryStream(signature.GetBytes()))
                    {
                        await package.AddSignatureAsync(stream, token);
                    }
                }
            }
            finally
            {
                FileUtility.Delete(tempPackagePath);
            }
        }

        public async Task RemoveSignatureAsync(CancellationToken token)
        {
            using (var packageReadStream = File.OpenRead(_options.PackageFilePath))
            using (var packageWriteStream = File.Open(_options.OutputFilePath, FileMode.OpenOrCreate))
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
        public Task SignAsync(CancellationToken token) => throw new NotImplementedException();

        /// <summary>
        /// Remove the primary signature from a package.
        /// </summary>
        public Task RemoveSignatureAsync(CancellationToken token) => throw new NotImplementedException();
#endif
    }
}