// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
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

            VerifyCertificate(request.Certificate);

            var zipArchiveHash = await _package.GetArchiveHashAsync(request.SignatureHashAlgorithm, token);

            var signatureContent = GenerateSignatureContent(request.SignatureHashAlgorithm, zipArchiveHash);

            // Create signature
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

            return new SignatureContent(hashAlgorithmName, base64ZipArchiveHash);
        }

        private void VerifyCertificate(X509Certificate2 certificate)
        {
            if (!SigningUtility.IsSignatureAlgorithmSupported(certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3022, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm);
            }

            if (!SigningUtility.IsCertificatePublicKeyValid(certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3023, Strings.SigningCertificateFailsPublicKeyLengthRequirement);
            }
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