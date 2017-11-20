// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

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
            {
                if (await IsSignedAsync(token))
                {
                    throw new SignatureException(Strings.SignedPackagePackageAlreadySigned);
                }

                ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Length);
                hashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                var signedCms = packageSignatureProvider.CreateSignedCms(hashAlgorithm.Hash, token);

                using (var writeZip = new ZipArchive(_zipWriteStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var signatureEntry = writeZip.CreateEntry(".signature", CompressionLevel.NoCompression);
                    using (var sigWriter = new BinaryWriter(signatureEntry.Open()))
                    {
                        sigWriter.Write(signedCms.Encode());
                    }

                    var checksignature = new SignedCms();
                    checksignature.Decode(signedCms.Encode());
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

        public static void ReadAndHashUntilPosition(BinaryReader reader, HashAlgorithm hashAlgorithm, long position)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (hashAlgorithm == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            var bufferSize = 4;
            while (reader.BaseStream.Position + bufferSize < position)
            {
                var bytes = reader.ReadBytes(bufferSize);
                HashBytes(hashAlgorithm, bytes);
            }
            var remainingBytes = position - reader.BaseStream.Position;
            if (remainingBytes > 0)
            {
                var bytes = reader.ReadBytes((int)remainingBytes);
                HashBytes(hashAlgorithm, bytes);
            }
        }

        /// <summary>
        /// Hashes given byte array with a specified HashAlgorithm
        /// </summary>
        /// <param name="hashAlgorithm">HashAlgorithm used to hash contents</param>
        /// <param name="bytes">Content to hash</param>
        public static void HashBytes(HashAlgorithm hashAlgorithm, byte[] bytes)
        {
            hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, outputBuffer: null, outputOffset: 0);
        }
#endif
    }
}
