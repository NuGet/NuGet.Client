// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Packaging.Signing
{
    internal static class SignedPackageArchiveIOUtility
    {
        internal const uint CentralDirectoryHeaderSignature = 0x02014b50;
        internal const uint EndOfCentralDirectorySignature = 0x06054b50;
        internal const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
        internal const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
        internal const uint LocalFileHeaderSignature = 0x04034b50;

        private static readonly SigningSpecifications _signingSpecification = SigningSpecifications.V1;

        /// <summary>
        /// Takes a binary reader and moves forwards the current position of it's base stream until it finds the specified signature.
        /// </summary>
        /// <param name="reader">Binary reader to update current position</param>
        /// <param name="byteSignature">byte signature to be matched</param>
        public static void SeekReaderForwardToMatchByteSignature(BinaryReader reader, byte[] byteSignature)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var stream = reader.BaseStream;

            if (stream.Position + byteSignature.Length > stream.Length)
            {
                throw new Exception($"Byte signature too big to seek in curren stream position.");
            }

            while (stream.Position != (stream.Length - byteSignature.Length))
            {
                if (CurrentStreamPositionMatchesByteSignature(reader, byteSignature))
                {
                    return;
                }

                stream.Position += 1;
            }

            throw new Exception($"Byte signature not found in zip: {BitConverter.ToString(byteSignature)}");
        }

        /// <summary>
        /// Read bytes from a BinaryReader and hash them with a given HashAlgorithm and stop when the provided position
        /// is the current position of the BinaryReader's base stream. It does not hash the byte in the provided position.
        /// </summary>
        /// <param name="reader">Read bytes from this stream</param>
        /// <param name="hashAlgorithm">HashAlgorithm used to hash contents</param>
        /// <param name="position">Position to stop copying data</param>
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
#if IS_DESKTOP
            hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, outputBuffer: null, outputOffset: 0);
#else
            throw new NotImplementedException();
#endif
        }

        /// <summary>
        /// Read ZIP's offsets and positions of offsets.
        /// </summary>
        /// <param name="reader">binary reader to zip archive</param>
        /// <returns>zip metadata with offsets and positions</returns>
        internal static SignedPackageArchiveMetadata ReadSignedArchiveMetadata(BinaryReader reader)
        {
            var signatureLocalFileHeaderPosition = 0L;
            var signatureFileCompressedSize = 0U;
            var signatureHasDataDescriptor = true;
            var signatureCentralDirectoryHeaderPosition = 0L;

            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(LocalFileHeaderSignature));

            // Read local file headers until signature is found
            var fileHeaderExtraSize = 0;
            var possibleSignaturePosition = reader.BaseStream.Position;
            var possibleSignature = reader.ReadUInt32();
            var hasFoundSignature = false;
            while (possibleSignature == LocalFileHeaderSignature)
            {
                // Jump to file name length
                reader.BaseStream.Seek(offset: 14, origin: SeekOrigin.Current);

                var possibleSignatureCompressedSize = reader.ReadUInt32();

                // Skip uncompressed size
                reader.BaseStream.Seek(offset: 4, origin: SeekOrigin.Current);

                var filenameLength = reader.ReadUInt16();
                var extraFieldlength = reader.ReadUInt16();

                var filename = reader.ReadBytes(filenameLength);
                var filenameString = Encoding.UTF8.GetString(filename);
                if (string.Equals(filenameString, _signingSpecification.SignaturePath))
                {
                    signatureFileCompressedSize = possibleSignatureCompressedSize;
                    signatureLocalFileHeaderPosition = possibleSignaturePosition;
                    hasFoundSignature = true;
                    fileHeaderExtraSize = filenameLength + extraFieldlength;
                    break;
                }
                // Skip extra field and data
                reader.BaseStream.Seek(offset: extraFieldlength + possibleSignatureCompressedSize, origin: SeekOrigin.Current);

                possibleSignaturePosition = reader.BaseStream.Position;
                possibleSignature = reader.ReadUInt32();
            }

            // TODO: Is it safe to asume we don't have an encryption header?
            // TODO: Is it safe to asume we don't have a data descriptor?
            // TODO: Is it safe to asume we don't have Archive Decryption Header and Archive Extra data record?
            if (hasFoundSignature && possibleSignature == LocalFileHeaderSignature || possibleSignature == CentralDirectoryHeaderSignature)
            {
                signatureHasDataDescriptor = false;
            }

            if (possibleSignature != CentralDirectoryHeaderSignature)
            {
                SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeaderSignature));
                possibleSignature = reader.ReadUInt32();
            }

            var possibleSignatureCentralDirectoryHeader = reader.BaseStream.Position - 4;
            // Look for signature central directory record
            while (possibleSignature == CentralDirectoryHeaderSignature)
            {
                // Skip until file name length
                reader.BaseStream.Seek(offset: 24, origin: SeekOrigin.Current);
                var filenameLength = reader.ReadUInt16();
                var extraFieldLength = reader.ReadUInt16();
                var fileCommentLength = reader.ReadUInt16();

                // Skip to read file name
                reader.BaseStream.Seek(offset: 12, origin: SeekOrigin.Current);

                var filename = reader.ReadBytes(filenameLength);
                var filenameString = Encoding.UTF8.GetString(filename);
                if (string.Equals(filenameString, _signingSpecification.SignaturePath))
                {
                    signatureCentralDirectoryHeaderPosition = possibleSignatureCentralDirectoryHeader;
                }

                // Read until end of central directory header
                var extraByteSize = extraFieldLength + fileCommentLength;
                reader.BaseStream.Seek(offset: extraByteSize, origin: SeekOrigin.Current);

                possibleSignatureCentralDirectoryHeader = reader.BaseStream.Position;
                possibleSignature = reader.ReadUInt32();
            }

            var isZip64 = possibleSignature == Zip64EndOfCentralDirectorySignature;

            var zip64EndOfCentralDirectoryRecordPosition = 0L;
            var zip64EndOfCentralDirectoryLocatorPosition = 0L;

            if (isZip64)
            {
                zip64EndOfCentralDirectoryRecordPosition = reader.BaseStream.Position;
                SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(Zip64EndOfCentralDirectoryLocatorSignature));
                zip64EndOfCentralDirectoryLocatorPosition = reader.BaseStream.Position;
            }

            var positionOffset = 0;
            if (possibleSignature != EndOfCentralDirectorySignature)
            {
                SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(EndOfCentralDirectorySignature));
            }
            else
            {
                positionOffset = 4;
            }

            var endOfCentralDirectoryRecordPosition = reader.BaseStream.Position - positionOffset;

            return new SignedPackageArchiveMetadata()
            {
                SignatureLocalFileHeaderPosition = signatureLocalFileHeaderPosition,
                SignatureFileCompressedSize = signatureFileCompressedSize,
                SignatureHasDataDescriptor = signatureHasDataDescriptor,
                SignatureCentralDirectoryHeaderPosition = signatureCentralDirectoryHeaderPosition,
                SignatureFileHeaderExtraSize = fileHeaderExtraSize,
                IsZip64 = isZip64,
                Zip64EndOfCentralDirectoryRecordPosition = zip64EndOfCentralDirectoryRecordPosition,
                Zip64EndOfCentralDirectoryLocatorPosition = zip64EndOfCentralDirectoryLocatorPosition,
                EndOfCentralDirectoryRecordPosition = endOfCentralDirectoryRecordPosition
            };
        }

        private static bool CurrentStreamPositionMatchesByteSignature(BinaryReader reader, byte[] byteSignature)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var stream = reader.BaseStream;

            for (var i = 0; i < byteSignature.Length; ++i)
            {
                var @byte = stream.ReadByte();

                if (@byte != byteSignature[i])
                {
                    stream.Seek(offset: -(i + 1), origin: SeekOrigin.Current);
                    return false;
                }

                if (i == byteSignature.Length - 1)
                {
                    stream.Seek(offset: -(byteSignature.Length), origin: SeekOrigin.Current);
                    return true;
                }
            }

            return false;
        }
    }
}
