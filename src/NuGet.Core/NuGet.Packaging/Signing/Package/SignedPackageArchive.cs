// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A nupkg that supports both reading and writing signatures.
    /// </summary>
    public class SignedPackageArchive : PackageArchiveReader, ISignedPackage
    {
        private readonly Stream _zipWriteStream;
        private readonly Stream _zipReadStream;
        private readonly SigningSpecifications _signingSpecification = SigningSpecifications.V1;

        public Stream ZipWriteStream => _zipWriteStream;

        public Stream ZipReadStream => _zipReadStream;

        public SignedPackageArchive(Stream packageReadStream, Stream packageWriteStream)
            : base(packageReadStream)
        {
            _zipWriteStream = packageWriteStream ?? throw new ArgumentNullException(nameof(packageWriteStream));
            _zipReadStream = packageReadStream ?? throw new ArgumentNullException(nameof(packageReadStream));
        }

        /// <summary>
        /// Adds a signature to a package if it is not already signed.
        /// </summary>
        /// <param name="signatureStream">Stream of the signature SignedCms object to be added to the package.</param>
        /// <param name="token">Cancellation Token.</param>
        /// <returns></returns>
        public async Task AddSignatureAsync(Stream signatureStream, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (_zipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackagePackageAlreadySigned);
            }

            using (var writeZip = new ZipArchive(_zipWriteStream, ZipArchiveMode.Update, leaveOpen: true))
            {
                var signatureEntry = writeZip.CreateEntry(_signingSpecification.SignaturePath, CompressionLevel.NoCompression);
                using (var signatureEntryStream = signatureEntry.Open())
                {
                    signatureStream.CopyTo(signatureEntryStream);
                }
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

            if (!await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnRemove);
            }

            using (var writeZip = new ZipArchive(_zipWriteStream, ZipArchiveMode.Update, leaveOpen: true))
            {
                writeZip.GetEntry(_signingSpecification.SignaturePath).Delete();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _zipWriteStream.Dispose();                
            }

            base.Dispose(disposing);
        }
    }
}
