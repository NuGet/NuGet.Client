// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

using System.Security.Cryptography.X509Certificates;
using System.Threading;

#if IS_DESKTOP
using Microsoft.ZipSigningUtilities;
#endif

using NuGet.Common;

namespace NuGet.Packaging.Signing
{

#if IS_DESKTOP
    /// <summary>
    /// Implements IZipSignatureProvider to allow signing packages using zip signing utility.
    /// </summary>
    public class PackageSignatureProvider : IZipSignatureProvider
    {
        private const ushort _majorVersion = 0; 
        private const ushort _minorVersion = 9;

        private readonly byte[] _nugetPackageSignatureFormatV1 = new byte[] { 0x81, 0x14, 0xAA, 0x4F, 0x08, 0x5B, 0x43, 0xAF, 0xAC, 0xD0, 0x49, 0xD1, 0xE1, 0x6E, 0x2F, 0x2C };
        private readonly ISignatureProvider _signatureProvider;
        private readonly SignPackageRequest _request;
        private readonly ILogger _logger;

        public PackageSignatureProvider(ISignatureProvider signatureProvider, SignPackageRequest request, ILogger logger)
        {
            _signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a signed Cms object from a zip archive hash.
        /// </summary>
        /// <param name="zipArchiveHash">byte[] hash of the package to be embedded in the signedCms.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A signedCms object that contains the zipArchiveHash in its Content field.</returns>
        public SignedCms CreateSignedCms(byte[] zipArchiveHash, CancellationToken token)
        {
            if (zipArchiveHash == null)
            {
                throw new ArgumentNullException(nameof(zipArchiveHash));
            }

            var signatureManifest = GenerateSignatureManifest(_request.SignatureHashAlgorithm, zipArchiveHash);
            var signature = _signatureProvider.CreateSignatureAsync(_request, signatureManifest, _logger, token).Result;

            return signature.SignedCms;
        }

        /// <summary>
        /// Creates a ZipSignatureHeader.
        /// </summary>
        /// <returns>ZipSignatureHeader that can be used while signing.</returns>
        public ZipSignatureHeader CreateZipHeader(uint signatureBlockSize, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return new ZipSignatureHeader(_nugetPackageSignatureFormatV1, _majorVersion, _minorVersion, signatureBlockSize);
        }

        private SignatureManifest GenerateSignatureManifest(HashAlgorithmName hashAlgorithmName, byte[] zipArchiveHash)
        {
            var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);

            return new SignatureManifest(hashAlgorithmName, base64ZipArchiveHash);
        }
    }
#endif
}
