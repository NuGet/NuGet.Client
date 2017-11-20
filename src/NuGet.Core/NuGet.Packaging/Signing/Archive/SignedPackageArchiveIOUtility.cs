// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
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
            var originalPosition = stream.Position;

            if (originalPosition + byteSignature.Length > stream.Length)
            {
                throw new Exception(Strings.ErrorByteSignatureTooBig);
            }

            while (stream.Position != (stream.Length - byteSignature.Length))
            {
                if (CurrentStreamPositionMatchesByteSignature(reader, byteSignature))
                {
                    return;
                }

                stream.Position += 1;
            }

            stream.Seek(offset: originalPosition, origin: SeekOrigin.Begin);
            throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.ErrorByteSignatureNotFound, BitConverter.ToString(byteSignature)));
        }

        /// <summary>
        /// Takes a binary reader and moves backwards the current position of it's base stream until it finds the specified signature.
        /// </summary>
        /// <param name="reader">Binary reader to update current position</param>
        /// <param name="byteSignature">byte signature to be matched</param>
        public static void SeekReaderBackwardToMatchByteSignature(BinaryReader reader, byte[] byteSignature)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var stream = reader.BaseStream;
            var originalPosition = stream.Position;

            if (originalPosition + byteSignature.Length > stream.Length)
            {
                throw new Exception(Strings.ErrorByteSignatureTooBig);
            }

            while (stream.Position != 0)
            {
                if (CurrentStreamPositionMatchesByteSignature(reader, byteSignature))
                {
                    return;
                }

                stream.Position -= 1;
            }

            stream.Seek(offset: originalPosition, origin: SeekOrigin.Begin);
            throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.ErrorByteSignatureNotFound, BitConverter.ToString(byteSignature)));
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
        /// <returns>metadata with offsets and positions for entries</returns>
        internal static SignedPackageArchiveMetadata ReadSignedArchiveMetadata(BinaryReader reader)
        {
            var metadata = new SignedPackageArchiveMetadata();

            // Look for EOCD signature, typically is around 22 bytes from the end
            reader.BaseStream.Seek(offset: -22, origin: SeekOrigin.End);
            SeekReaderBackwardToMatchByteSignature(reader, BitConverter.GetBytes(EndOfCentralDirectorySignature));
            metadata.EndOfCentralDirectoryRecordPosition = reader.BaseStream.Position;

            // Jump to offset of start of central directory
            reader.BaseStream.Seek(offset: 16, origin: SeekOrigin.Current);
            var offsetOfStartOfCD = reader.ReadUInt32();

            // Look for signature central directory record
            reader.BaseStream.Seek(offset: offsetOfStartOfCD, origin: SeekOrigin.Begin);

            var hasFoundSignatureEntry = false;
            while (!hasFoundSignatureEntry)
            {
                var possibleSignatureCentralDirectoryRecordPosition = reader.BaseStream.Position;

                // Skip until file name length
                reader.BaseStream.Seek(offset: 28, origin: SeekOrigin.Current);
                var filenameLength = reader.ReadUInt16();
                var extraFieldLength = reader.ReadUInt16();
                var fileCommentLength = reader.ReadUInt16();

                // Skip to read local header offset
                reader.BaseStream.Seek(offset: 8, origin: SeekOrigin.Current);

                var localHeaderOffset = reader.ReadUInt32();

                var filename = reader.ReadBytes(filenameLength);
                var filenameString = Encoding.UTF8.GetString(filename);
                if (string.Equals(filenameString, _signingSpecification.SignaturePath))
                {
                    hasFoundSignatureEntry = true;

                    metadata.SignatureCentralDirectoryHeaderPosition = possibleSignatureCentralDirectoryRecordPosition;
                    metadata.SignatureCentralDirectoryEntrySize = 46 + filenameLength + extraFieldLength + fileCommentLength;

                    metadata.SignatureLocalFileHeaderPosition = localHeaderOffset;

                    // Go to local file header and skip signature
                    reader.BaseStream.Seek(offset: metadata.SignatureLocalFileHeaderPosition + 4, origin: SeekOrigin.Begin);

                    // The total size of file entry is from the start of the file header until
                    // the start of the next file header (or the start of the first central directory header)
                    try
                    {
                        SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(LocalFileHeaderSignature));
                    }
                    // No local File header found (the signature file must be the last entry), search for the start of the first central directory
                    catch
                    {
                        SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeaderSignature));
                    }

                    metadata.SignatureFileEntryTotalSize = reader.BaseStream.Position - metadata.SignatureLocalFileHeaderPosition;
                }

                try
                {
                    SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeaderSignature));
                }
                catch
                {
                    break;
                }
            }

            if (!hasFoundSignatureEntry)
            {
                throw new Exception(Strings.ErrorPackageNotSigned);
            }

            try
            {
                SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(Zip64EndOfCentralDirectorySignature));

                metadata.IsZip64 = true;
                metadata.Zip64EndOfCentralDirectoryRecordPosition = reader.BaseStream.Position;
            }
            catch { }

            return metadata;
        }

        private static bool CurrentStreamPositionMatchesByteSignature(BinaryReader reader, byte[] byteSignature)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var stream = reader.BaseStream;

            if (stream.Length < byteSignature.Length)
            {
                return false;
            }

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
