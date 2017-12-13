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

        /// <summary>
        /// Stream underlying the ZipArchive. Used to do signature verification on a SignedPackageArchive.
        /// If this is null then we cannot perform signature verification.
        /// </summary>
        private Stream ZipWriteStream { get; set; }

        public SignedPackageArchive(Stream packageReadStream, Stream packageWriteStream)
            : base(new ZipArchive(packageReadStream, ZipArchiveMode.Read, leaveOpen: true), DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
            ZipReadStream = packageReadStream ?? throw new ArgumentNullException(nameof(packageReadStream));
            ZipWriteStream = packageWriteStream ?? throw new ArgumentNullException(nameof(packageWriteStream));
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

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackagePackageAlreadySigned);
            }

            using (var reader = new BinaryReader(ZipReadStream, SigningSpecifications.Encoding, leaveOpen: true))
            using (var writer = new BinaryWriter(ZipWriteStream, SigningSpecifications.Encoding, leaveOpen: true))
            {
                var packageMetadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                var signatureDateTime = DateTime.Now;

                var memoryStream = signatureStream as MemoryStream;
                var signatureBytes = memoryStream.ToArray();

                // ensure both streams are reset
                reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
                writer.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);

                // copy all data till previous end of local file headers
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfFileHeaders);

                // write the signature local file header
                var signatureFileHeaderLength = SignedPackageArchiveIOUtility.WriteFileHeader(writer, signatureBytes, signatureDateTime);

                // write the signature file
                var signatureFileLength = SignedPackageArchiveIOUtility.WriteFile(writer, signatureBytes);

                // copy all data that was after previous end of local file headers till previous end of central directory headers
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfCentralDirectory);

                // write the central directory header for signature file
                var signatureCentralDirectoryHeaderLength = SignedPackageArchiveIOUtility.WriteCentralDirectoryHeader(writer, signatureBytes, signatureDateTime, packageMetadata.EndOfFileHeaders);

                // copy all data that was after previous end of central directory headers till previous start of end of central directory record
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfCentralDirectoryRecordPosition);

                var totalSignatureSize = signatureFileHeaderLength + signatureFileLength;

                // update and write the end of central directory record
                SignedPackageArchiveIOUtility.WriteEndOfCentralDirectoryRecord(reader, writer, signatureCentralDirectoryHeaderLength, totalSignatureSize);
            }
        }

        /// <summary>
        /// Remove a signature from the package, if it exists.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task RemoveSignatureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ZipReadStream == null)
            {
                throw new SignatureException(Strings.SignedPackageUnableToAccessSignature);
            }

            if (!await IsSignedAsync(token))
            {
                throw new SignatureException(Strings.SignedPackageNotSignedOnRemove);
            }

            using (var zip = new ZipArchive(ZipReadStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                zip.GetEntry(_signingSpecification.SignaturePath).Delete();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
