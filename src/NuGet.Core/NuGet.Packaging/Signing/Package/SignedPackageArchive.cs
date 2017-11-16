// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if IS_DESKTOP
using Microsoft.ZipSigningUtilities;
#endif

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A nupkg that supports both reading and writing signatures.
    /// </summary>
    public class SignedPackageArchive : PackageArchiveReader, ISignedPackage
    {
        private readonly Stream _zipWriteStream;
        private readonly Stream _zipReadStream;
        private readonly Encoding _utf8Encoding = new UTF8Encoding();


        public Stream ZipWriteStream => _zipWriteStream;

        public Stream ZipReadStream => _zipReadStream;

        public SignedPackageArchive(Stream packageReadStream, Stream packageWriteStream)
            : base(packageReadStream)
        {
            _zipWriteStream = packageWriteStream ?? throw new ArgumentNullException(nameof(packageWriteStream));
            _zipReadStream = packageReadStream ?? throw new ArgumentNullException(nameof(packageReadStream));
        }

#if IS_DESKTOP
        /// <summary>
        /// Adds signature to a package
        /// </summary>
        /// <param name="packageSignatureProvider">A signature provider that can be used to generate a SignedCms object.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task AddSignatureAsync(IZipSignatureProvider packageSignatureProvider, HashAlgorithm hashAlgorithm, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (packageSignatureProvider == null)
            {
                throw new ArgumentNullException(nameof(packageSignatureProvider));
            }

            if (_zipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            using (var reader = new BinaryReader(_zipReadStream, _utf8Encoding, leaveOpen: true))
            using (var writer = new BinaryWriter(_zipWriteStream, _utf8Encoding, leaveOpen: true))
            {
                if (await IsSignedAsync(token))
                {
                    throw new SignatureException(Strings.SignedPackagePackageAlreadySigned);
                }

                ZipSigningUtilities.Sign(reader, writer, hashAlgorithm, packageSignatureProvider);
            }
        }

        /// <summary>
        /// Remove a signature from the package, if it exists.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task RemoveSignatureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (_zipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            using (var reader = new BinaryReader(_zipReadStream, _utf8Encoding, leaveOpen: true))
            using (var writer = new BinaryWriter(_zipWriteStream, _utf8Encoding, leaveOpen: true))
            {
                if (!await IsSignedAsync(token))
                {
                    throw new SignatureException(Strings.SignedPackagePackageNotSigned);
                }

                ZipSigningUtilities.RemoveSignature(reader, writer);
            }
        }
#endif
    }
}
