// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A nupkg that supports both reading and writing signatures.
    /// </summary>
    public class SignedPackageArchive : PackageArchiveReader, ISignedPackage
    {
        private readonly SigningSpecifications _signingSpecification = SigningSpecifications.V1;

        public SignedPackageArchive(Stream packageStream)
            : base(new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true), DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
            ZipStream = packageStream ?? throw new ArgumentNullException(nameof(packageStream));
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

            if (ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackagePackageAlreadySigned);
            }

            var signatureEntry = Zip.CreateEntry(_signingSpecification.SignaturePath, CompressionLevel.NoCompression);
            using (var signatureEntryStream = signatureEntry.Open())
            {
                signatureStream.CopyTo(signatureEntryStream);
            }
        }

        /// <summary>
        /// Remove a signature from the package, if it exists.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task RemoveSignatureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (!await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnRemove);
            }

            Zip.GetEntry(_signingSpecification.SignaturePath).Delete();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
