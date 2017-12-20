// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public static class SignedPackageArchiveIOUtility
    {
        // Local file header size excluding signature, file name and extra field
        internal const uint LocalFileHeaderSizeWithoutSignature = 26;

        // All the signatures have a fixed length of 4 bytes
        internal const uint ZipHeaderSignatureSize = 4;
        internal const uint EndOfCentralDirectorySignature = 0x06054b50;
        internal const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
        internal const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
        internal const uint LocalFileHeaderSignature = 0x04034b50;

        private static readonly SigningSpecifications _signingSpecification = SigningSpecifications.V1;

        // used while converting DateTime to MS-DOS date time format
        private const int ValidZipDate_YearMin = 1980;
        private const int ValidZipDate_YearMax = 2107;

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

            if (byteSignature == null || byteSignature.Length == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(byteSignature));
            }

            var stream = reader.BaseStream;
            var originalPosition = stream.Position;

            if (originalPosition + byteSignature.Length > stream.Length)
            {
                throw new ArgumentOutOfRangeException(Strings.ErrorByteSignatureTooBig);
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
            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorByteSignatureNotFound, BitConverter.ToString(byteSignature)));
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

            if (byteSignature == null || byteSignature.Length == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(byteSignature));
            }

            var stream = reader.BaseStream;
            var originalPosition = stream.Position;

            if (originalPosition + byteSignature.Length > stream.Length)
            {
                throw new ArgumentOutOfRangeException(Strings.ErrorByteSignatureTooBig);
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
            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorByteSignatureNotFound, BitConverter.ToString(byteSignature)));
        }

        /// <summary>
        /// Read bytes from a BinaryReader and write them to the BinaryWriter and stop when the provided position
        /// is the current position of the BinaryReader's base stream. It does not read the byte in the provided position.
        /// </summary>
        /// <param name="reader">Read bytes from this stream.</param>
        /// <param name="writer">Write bytes to this stream.</param>
        /// <param name="position">Position to stop copying data.</param>
        public static void ReadAndWriteUntilPosition(BinaryReader reader, BinaryWriter writer, long position)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (position > reader.BaseStream.Length)
            {
                throw new ArgumentException(Strings.SignedPackageArchiveIOExtraRead, nameof(position));
            }

            if (position < reader.BaseStream.Position)
            {
                throw new ArgumentException(Strings.SignedPackageArchiveIOInvalidRead, nameof(position));
            }

            var bufferSize = 4;
            while (reader.BaseStream.Position + bufferSize < position)
            {
                var bytes = reader.ReadBytes(bufferSize);
                writer.Write(bytes);
            }
            var remainingBytes = position - reader.BaseStream.Position;
            if (remainingBytes > 0)
            {
                var bytes = reader.ReadBytes((int)remainingBytes);
                writer.Write(bytes);
            }
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

            if (position > reader.BaseStream.Length)
            {
                throw new ArgumentException(Strings.SignedPackageArchiveIOExtraRead, nameof(position));
            }

            if (position < reader.BaseStream.Position)
            {
                throw new ArgumentException(Strings.SignedPackageArchiveIOInvalidRead, nameof(position));
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
            if (hashAlgorithm == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(bytes));
            }
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
        public static SignedPackageArchiveMetadata ReadSignedArchiveMetadata(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var metadata = new SignedPackageArchiveMetadata()
            {
                StartOfFileHeaders = reader.BaseStream.Length,
            };
            var centralDirectoryRecords = new List<CentralDirectoryHeaderMetadata>();

            // Look for EOCD signature, typically is around 22 bytes from the end
            reader.BaseStream.Seek(offset: -22, origin: SeekOrigin.End);
            SeekReaderBackwardToMatchByteSignature(reader, BitConverter.GetBytes(EndOfCentralDirectorySignature));
            metadata.EndOfCentralDirectoryRecordPosition = reader.BaseStream.Position;

            // Jump to offset of start of central directory, 16 bytes from the start of EOCD (including signature length)
            reader.BaseStream.Seek(offset: 16, origin: SeekOrigin.Current);
            metadata.StartOfCentralDirectory = reader.ReadUInt32();
            reader.BaseStream.Seek(offset: metadata.StartOfCentralDirectory, origin: SeekOrigin.Begin);

            // Read central directory records
            var possibleSignatureCentralDirectoryRecordPosition = reader.BaseStream.Position;
            CentralDirectoryHeader header;

            while (CentralDirectoryHeader.TryRead(reader, out header))
            {
                if (header.RelativeOffsetOfLocalHeader < metadata.StartOfFileHeaders)
                {
                    metadata.StartOfFileHeaders = header.RelativeOffsetOfLocalHeader;
                }

                var isUtf8 = SignedPackageArchiveUtility.IsUtf8(header.GeneralPurposeBitFlag);

                var centralDirectoryMetadata = new CentralDirectoryHeaderMetadata()
                {
                    Filename = SignedPackageArchiveUtility.GetString(header.FileName, isUtf8),
                    HeaderSize = header.GetSizeInBytes(),
                    OffsetToFileHeader = header.RelativeOffsetOfLocalHeader,
                    Position = possibleSignatureCentralDirectoryRecordPosition
                };

                centralDirectoryRecords.Add(centralDirectoryMetadata);

                possibleSignatureCentralDirectoryRecordPosition = reader.BaseStream.Position;
            }

            if (centralDirectoryRecords.Count == 0)
            {
                throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
            }

            var lastCentralDirectoryRecord = centralDirectoryRecords.Last();
            metadata.EndOfCentralDirectory = lastCentralDirectoryRecord.Position + lastCentralDirectoryRecord.HeaderSize;
            metadata.CentralDirectoryHeaders = centralDirectoryRecords;

            // Update signed package archive metadata
            UpdateSignedPackageArchiveMetadata(reader, metadata);

            // Make sure the package is not zip64
            var isZip64 = false;
            try
            {
                SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(Zip64EndOfCentralDirectorySignature));
                isZip64 = true;
            }
            // Failure means package is not a zip64 archive, then is safe to ignore.
            catch { }

            if (isZip64)
            {
                throw new InvalidDataException(Strings.ErrorZip64NotSupported);
            }

            return metadata;
        }

        /// <summary>
        /// Updates the SignedPackageArchiveMetadata.CentralDirectoryHeaders by updating IndexInHeaders and FileEntryTotalSize.
        /// Updates the SignedPackageArchiveMetadata.EndOfFileHeaders.
        /// </summary>
        /// <param name="reader">Binary reader to zip archive.</param>
        /// <param name="metadata">SignedPackageArchiveMetadata to be updated.</param>
        public static void UpdateSignedPackageArchiveMetadata(
            BinaryReader reader,
            SignedPackageArchiveMetadata metadata)
        {
            // Get missing metadata for central directory records
            var centralDirectoryRecords = metadata.CentralDirectoryHeaders;
            var centralDirectoryRecordsCount = centralDirectoryRecords.Count;
            var endOfAllFileHeaders = 0L;

            for (var centralDirectoryRecordIndex = 0; centralDirectoryRecordIndex < centralDirectoryRecordsCount; centralDirectoryRecordIndex++)
            {
                var record = centralDirectoryRecords[centralDirectoryRecordIndex];

                if (StringComparer.Ordinal.Equals(record.Filename, _signingSpecification.SignaturePath))
                {
                    metadata.SignatureCentralDirectoryHeaderIndex = centralDirectoryRecordIndex;
                }

                // Go to local file header
                reader.BaseStream.Seek(offset: record.OffsetToFileHeader, origin: SeekOrigin.Begin);

                // Validate file header signature
                var fileHeaderSignature = reader.ReadUInt32();

                if (fileHeaderSignature != LocalFileHeaderSignature)
                {
                    throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
                }

                // The total size of file entry is from the start of the file header until
                // the start of the next file header (or the start of the first central directory header)
                try
                {
                    SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(LocalFileHeaderSignature));
                }
                // No local File header found (entry must be the last entry), search for the start of the first central directory
                catch
                {
                    SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeader.Signature));
                }

                record.IndexInHeaders = centralDirectoryRecordIndex;
                record.FileEntryTotalSize = reader.BaseStream.Position - record.OffsetToFileHeader;

                var endofFileHeader = record.FileEntryTotalSize + record.OffsetToFileHeader;

                if (endofFileHeader > endOfAllFileHeaders)
                {
                    endOfAllFileHeaders = endofFileHeader;
                }
            }

            metadata.EndOfFileHeaders = endOfAllFileHeaders;
        }

        /// <summary>
        /// Asserts that the SignedPackageArchiveMetadata contains only one Signature file entry.
        /// Updates SignedPackageArchiveMetadata.SignatureCentralDirectoryHeaderIndex with the index of the signature central directory header.
        /// Throws SignatureException if less or more entries are found.
        /// </summary>
        /// <param name="metadata">SignedPackageArchiveMetadata to be checked for signature entry.</param>
        internal static void AssertExactlyOnePrimarySignatureAndUpdateMetadata(SignedPackageArchiveMetadata metadata)
        {
            // Get missing metadata for central directory records
            var hasFoundSignature = false;
            var centralDirectoryRecords = metadata.CentralDirectoryHeaders;
            var centralDirectoryRecordsCount = centralDirectoryRecords.Count;

            for (var centralDirectoryRecordIndex = 0; centralDirectoryRecordIndex < centralDirectoryRecordsCount; centralDirectoryRecordIndex++)
            {
                var record = centralDirectoryRecords[centralDirectoryRecordIndex];

                if (StringComparer.Ordinal.Equals(record.Filename, _signingSpecification.SignaturePath))
                {
                    if (hasFoundSignature)
                    {
                        throw new SignatureException(Strings.Error_NotOnePrimarySignature);
                    }

                    metadata.SignatureCentralDirectoryHeaderIndex = centralDirectoryRecordIndex;
                    hasFoundSignature = true;
                }
            }

            if (!hasFoundSignature)
            {
                throw new SignatureException(Strings.Error_NotOnePrimarySignature);
            }
        }

        /// <summary>
        /// Asserts the validity of local file header and central directory file header for the signature file in the package.
        /// Throws Signature Exception if the headers are invalid.
        /// </summary>
        /// <param name="reader">BinaryReader on the package.</param>
        /// <param name="signatureCentralDirectoryHeader">Metadata of the the package.</param>
        public static void AssertSignatureEntryMetadata(BinaryReader reader, CentralDirectoryHeaderMetadata signatureCentralDirectoryHeader)
        {
            // skip header signature (4 bytes) and version fields (2 entires of 2 bytes)
            reader.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 8L, origin: SeekOrigin.Begin);

            // check central directory file header
            AssertSignatureEntryHeaderMetadata(reader, signatureCentralDirectoryHeader, Strings.SignatureCentralDirectoryHeaderInvalid);

            // Skip file name length (2 bytes), extra field length (2 bytes), file comment length (2 bytes),
            // disk number start (2 bytes), and internal file attributes (2 bytes)
            reader.BaseStream.Seek(offset: 10, origin: SeekOrigin.Current);

            var externalFileAttributes = reader.ReadUInt32();
            AssertValue(
                expectedValue: 0U,
                actualValue: externalFileAttributes,
                errorCode: NuGetLogCode.NU3011,
                errorMessagePrefix: Strings.SignatureCentralDirectoryHeaderInvalid,
                errorMessageSuffix: Strings.SignatureInvalidExternalFileAttributes);

            // skip header signature (4 bytes) and version field (2 bytes)
            reader.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToFileHeader + 6L, origin: SeekOrigin.Begin);

            // check local file header
            AssertSignatureEntryHeaderMetadata(reader, signatureCentralDirectoryHeader, Strings.SignatureLocalFileHeaderInvalid);
        }

        private static void AssertSignatureEntryHeaderMetadata(BinaryReader reader, CentralDirectoryHeaderMetadata signatureCentralDirectoryHeader, string errorPrefix)
        {
            var signatureEntryErrorCode = NuGetLogCode.NU3011;

            // Assert general purpose bits to 0
            uint actualValue = reader.ReadUInt16();
            AssertValue(
                expectedValue: 0U,
                actualValue: actualValue,
                errorCode: signatureEntryErrorCode,
                errorMessagePrefix: errorPrefix,
                errorMessageSuffix: Strings.SignatureInvalidGeneralPurposeBits);

            // Assert compression method to 0
            actualValue = reader.ReadUInt16();
            AssertValue(
                expectedValue: 0U,
                actualValue: actualValue,
                errorCode: signatureEntryErrorCode,
                errorMessagePrefix: errorPrefix,
                errorMessageSuffix: Strings.SignatureInvalidCompressionMethod);

            // skip date (2 bytes), time (2 bytes) and crc32 (4 bytes)
            reader.BaseStream.Seek(offset: 8L, origin: SeekOrigin.Current);

            // assert that file compressed and uncompressed sizes are the same
            var compressedSize = reader.ReadUInt32();
            var unCompressedSize = reader.ReadUInt32();
            if (compressedSize != unCompressedSize)
            {
                throw new SignatureException(
                    signatureEntryErrorCode,
                    string.Format(CultureInfo.CurrentCulture, errorPrefix, Strings.SignatureFileCompressed));
            }
        }

        private static void AssertValue(
            uint expectedValue,
            uint actualValue,
            NuGetLogCode errorCode,
            string errorMessagePrefix,
            string errorMessageSuffix)
        {
            if (!actualValue.Equals(expectedValue))
            {
                var failureCause = string.Format(
                    CultureInfo.CurrentCulture,
                    errorMessageSuffix,
                    expectedValue,
                    actualValue);

                throw new SignatureException(
                    errorCode,
                    string.Format(CultureInfo.CurrentCulture, errorMessagePrefix, failureCause));
            }
        }

        /// <summary>
        /// Writes the signature data into the zip using the writer.
        /// The reader is used to read the exisiting zip. 
        /// </summary>
        /// <param name="signatureStream">MemoryStream of the signature to be inserted into the zip.</param>
        /// <param name="reader">BinaryReader to be used to read the existing zip data.</param>
        /// <param name="writer">BinaryWriter to be used to write the signature into the zip.</param>
        internal static void WriteSignatureIntoZip(
            MemoryStream signatureStream,
            BinaryReader reader,
            BinaryWriter writer)
        {
            if (signatureStream == null)
            {
                throw new ArgumentNullException(nameof(signatureStream));
            }

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            var packageMetadata = ReadSignedArchiveMetadata(reader);
            var signatureBytes = signatureStream.ToArray();
            var signatureCrc32 = Crc32.CalculateCrc(signatureBytes);
            var signatureDosTime = DateTimeToDosTime(DateTime.Now);

            // ensure both streams are reset
            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            writer.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);

            // copy all data till previous end of local file headers
            ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfFileHeaders);

            // write the signature local file header
            var signatureFileHeaderLength = WriteLocalFileHeaderIntoZip(writer, signatureBytes, signatureCrc32, signatureDosTime);

            // write the signature file
            var signatureFileLength = WriteFileIntoZip(writer, signatureBytes);

            // copy all data that was after previous end of local file headers till previous end of central directory headers
            ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfCentralDirectory);

            // write the central directory header for signature file
            var signatureCentralDirectoryHeaderLength = WriteCentralDirectoryHeaderIntoZip(writer, signatureBytes, signatureCrc32, signatureDosTime, packageMetadata.EndOfFileHeaders);

            // copy all data that was after previous end of central directory headers till previous start of end of central directory record
            ReadAndWriteUntilPosition(reader, writer, packageMetadata.EndOfCentralDirectoryRecordPosition);

            var totalSignatureSize = signatureFileHeaderLength + signatureFileLength;

            // update and write the end of central directory record
            ReadAndWriteUpdatedEndOfCentralDirectoryRecordIntoZip(reader, writer, signatureCentralDirectoryHeaderLength, totalSignatureSize);
        }

        /// <summary>
        /// Writes a local file header into a zip using the writer starting at the writer.BaseStream.Position.
        /// </summary>
        /// <param name="writer">BinaryWriter to be used to write file.</param>
        /// <param name="fileData">Byte[] of the corresponding file to be written into the zip.</param>
        /// <param name="crc32">CRC-32 for the file.</param>
        /// <param name="dosDateTime">Last modified DateTime for the file data.</param>
        /// <returns>Number of total bytes written into the zip.</returns>
        private static long WriteLocalFileHeaderIntoZip(
            BinaryWriter writer,
            byte[] fileData,
            uint crc32,
            uint dosDateTime)
        {
            // Write the file header signature
            writer.Write(LocalFileHeaderSignature);

            // Version needed to extract:  2.0
            writer.Write((ushort)20);

            // General purpose bit flags
            writer.Write((ushort)0);

            // The file is stored (no compression)
            writer.Write((ushort)0);

            // write date and time
            writer.Write(dosDateTime);

            // write file CRC32
            writer.Write(crc32);

            // write uncompressed size
            writer.Write((uint)fileData.Length);

            // write compressed size - same as uncompressed since file should have no compression
            writer.Write((uint)fileData.Length);

            // write file name length
            var fileNameBytes = Encoding.ASCII.GetBytes(_signingSpecification.SignaturePath);
            var fileNameLength = fileNameBytes.Length;
            writer.Write((ushort)fileNameLength);

            // write extra field length
            writer.Write((ushort)0);

            // write file name
            writer.Write(fileNameBytes);

            // calculate the total length of data written
            var writtenDataLength = LocalFileHeaderSizeWithoutSignature + ZipHeaderSignatureSize + fileNameLength;

            return writtenDataLength;
        }

        /// <summary>
        /// Writes a file into a zip using the writer starting at the writer.BaseStream.Position.
        /// </summary>
        /// <param name="writer">BinaryWriter to be used to write file.</param>
        /// <param name="fileData">Byte[] of the file to be written into the zip.</param>
        /// <returns>Number of total bytes written into the zip.</returns>
        private static long WriteFileIntoZip(BinaryWriter writer, byte[] fileData)
        {
            // write file
            writer.Write(fileData);

            // calculate the total length of data written
            var writtenDataLength = (long)fileData.Length;

            return writtenDataLength;
        }

        /// <summary>
        /// Writes a central directory header into a zip using the writer starting at the writer.BaseStream.Position.
        /// </summary>
        /// <param name="writer">BinaryWriter to be used to write file.</param>
        /// <param name="fileData">Byte[] of the file to be written into the zip.</param>
        /// <param name="crc32">CRC-32 checksum for the file.</param>
        /// <param name="dosDateTime">Last modified DateTime for the file data.</param>
        /// <param name="fileOffset">Offset, in bytes, for the local file header of the corresponding file from the start of the archive.</param>
        /// <returns>Number of total bytes written into the zip.</returns>
        private static long WriteCentralDirectoryHeaderIntoZip(
            BinaryWriter writer,
            byte[] fileData,
            uint crc32,
            uint dosDateTime,
            long fileOffset)
        {
            // Write the file header signature
            writer.Write(CentralDirectoryHeader.Signature);

            // Version made by:  2.0
            writer.Write((ushort)20);

            // Version needed to extract:  2.0
            writer.Write((ushort)20);

            // General purpose bit flags
            writer.Write((ushort)0);

            // The file is stored (no compression)
            writer.Write((ushort)0);

            // write date and time
            writer.Write(dosDateTime);

            // write file CRC32
            writer.Write(crc32);

            // write uncompressed size
            writer.Write((uint)fileData.Length);

            // write compressed size - same as uncompressed since file should have no compression
            writer.Write((uint)fileData.Length);

            // write file name length
            var fileNameBytes = Encoding.ASCII.GetBytes(_signingSpecification.SignaturePath);
            var fileNameLength = fileNameBytes.Length;
            writer.Write((ushort)fileNameLength);

            // write extra field length
            writer.Write((ushort)0);

            // write file comment length
            writer.Write((ushort)0);

            // write disk number start
            writer.Write((ushort)0);

            // write internal file attributes
            writer.Write((ushort)0);

            // write external file attributes
            writer.Write((uint)0);

            // write relative offset of local header
            writer.Write((uint)fileOffset);

            // write file name
            writer.Write(fileNameBytes);

            // calculate the total length of data written
            var writtenDataLength = CentralDirectoryHeader.SizeInBytesOfFixedLengthFields + ZipHeaderSignatureSize + fileNameLength;

            return writtenDataLength;
        }

        /// <summary>
        /// Writes the end of central directory header into a zip using the writer starting at the writer.BaseStream.Position.
        /// The new end of central directory record will be based on the one at reader.BaseStream.Position.
        /// </summary>
        /// <param name="reader">BinaryWriter to be used to read exisitng end of central directory record.</param>
        /// <param name="writer">BinaryWriter to be used to write file.</param>
        /// <param name="sizeOfSignatureCentralDirectoryRecord">Size of the central directory header for the signature file.</param>
        /// <param name="sizeOfSignatureFileHeaderAndData">Size of the signature file and the corresponding local file header.</param>
        private static void ReadAndWriteUpdatedEndOfCentralDirectoryRecordIntoZip(
            BinaryReader reader,
            BinaryWriter writer,
            long sizeOfSignatureCentralDirectoryRecord,
            long sizeOfSignatureFileHeaderAndData)
        {
            // 4 bytes for disk numbers. same as before.
            ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + 8L);

            // Update central directory header counts by adding 1 for the signature entry
            var centralDirectoryCountOnThisDisk = reader.ReadUInt16();
            writer.Write((ushort)(centralDirectoryCountOnThisDisk + 1));

            var centralDirectoryCountTotal = reader.ReadUInt16();
            writer.Write((ushort)(centralDirectoryCountTotal + 1));

            // Update size of central directory by adding size of signature central directory size
            var sizeOfCentralDirectory = reader.ReadUInt32();
            writer.Write((uint)(sizeOfCentralDirectory + sizeOfSignatureCentralDirectoryRecord));

            // update the offset of central directory by adding the size of the signature local file header and data
            var offsetOfCentralDirectory = reader.ReadUInt32();
            writer.Write((uint)(offsetOfCentralDirectory + sizeOfSignatureFileHeaderAndData));

            // read and write the rest of the data
            ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Length);
        }

        private static bool CurrentStreamPositionMatchesByteSignature(BinaryReader reader, byte[] byteSignature)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (byteSignature == null || byteSignature.Length == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(byteSignature));
            }

            var stream = reader.BaseStream;

            if (stream.Length < byteSignature.Length)
            {
                return false;
            }

            var startingOffset = stream.Position;

            for (var i = 0; i < byteSignature.Length; ++i)
            {
                var b = stream.ReadByte();

                if (b != byteSignature[i])
                {
                    stream.Seek(offset: startingOffset, origin: SeekOrigin.Begin);
                    return false;
                }
            }

            stream.Seek(offset: startingOffset, origin: SeekOrigin.Begin);

            return true;
        }

        /// <summary>
        /// Converts a DateTime value into a unit in the MS-DOS date time format.
        /// Reference - https://docs.microsoft.com/en-us/cpp/c-runtime-library/32-bit-windows-time-date-formats
        /// Reference - https://source.dot.net/#System.IO.Compression/System/IO/Compression/ZipHelper.cs,91
        /// </summary>
        /// <param name="dateTime">DateTime value to be converted.</param>
        /// <returns>uint representing the MS-DOS equivalent date time.</returns>
        private static uint DateTimeToDosTime(DateTime dateTime)
        {
            // DateTime must be Convertible to DosTime
            Debug.Assert(ValidZipDate_YearMin <= dateTime.Year && dateTime.Year <= ValidZipDate_YearMax);

            var ret = ((dateTime.Year - ValidZipDate_YearMin) & 0x7F);
            ret = (ret << 4) + dateTime.Month;
            ret = (ret << 5) + dateTime.Day;
            ret = (ret << 5) + dateTime.Hour;
            ret = (ret << 6) + dateTime.Minute;
            ret = (ret << 5) + (dateTime.Second / 2); // only 5 bits for second, so we only have a granularity of 2 sec.
            return (uint)ret;
        }
    }
}