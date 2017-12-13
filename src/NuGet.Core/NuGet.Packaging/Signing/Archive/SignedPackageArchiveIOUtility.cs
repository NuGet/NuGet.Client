// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Packaging.Signing
{
    public static class SignedPackageArchiveIOUtility
    {
        internal const uint CentralDirectoryHeaderSignature = 0x02014b50;
        internal const uint EndOfCentralDirectorySignature = 0x06054b50;
        internal const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
        internal const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
        internal const uint LocalFileHeaderSignature = 0x04034b50;

        // Central Directory file header size excluding signature, file name, extra field and file comment
        internal const uint CentralDirectoryFileHeaderSize = 46;

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
            var isReadingCentralDirectoryRecords = true;

            while (isReadingCentralDirectoryRecords)
            {
                var centralDirectoryMetadata = new CentralDirectoryHeaderMetadata
                {
                    Position = possibleSignatureCentralDirectoryRecordPosition
                };
                var centralDirectoryHeaderSignature = reader.ReadUInt32();
                if (centralDirectoryHeaderSignature != CentralDirectoryHeaderSignature)
                {
                    throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
                }

                // Skip until file name length, 24 bytes after signature of central directory record (excluding signature length)
                reader.BaseStream.Seek(offset: 24, origin: SeekOrigin.Current);
                var filenameLength = reader.ReadUInt16();
                var extraFieldLength = reader.ReadUInt16();
                var fileCommentLength = reader.ReadUInt16();

                // Skip to read local header offset (8 bytes after file comment length field)
                reader.BaseStream.Seek(offset: 8, origin: SeekOrigin.Current);

                centralDirectoryMetadata.OffsetToFileHeader = reader.ReadUInt32();

                if (centralDirectoryMetadata.OffsetToFileHeader < metadata.StartOfFileHeaders)
                {
                    metadata.StartOfFileHeaders = centralDirectoryMetadata.OffsetToFileHeader;
                }

                var filename = reader.ReadBytes(filenameLength);
                centralDirectoryMetadata.Filename = Encoding.ASCII.GetString(filename);
                // Save total size of central directory record + variable length fields
                centralDirectoryMetadata.HeaderSize = CentralDirectoryFileHeaderSize + filenameLength + extraFieldLength + fileCommentLength;

                try
                {
                    SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeaderSignature));
                    possibleSignatureCentralDirectoryRecordPosition = reader.BaseStream.Position;
                }
                catch
                {
                    isReadingCentralDirectoryRecords = false;
                }
                centralDirectoryRecords.Add(centralDirectoryMetadata);
            }

            var lastCentralDirectoryRecord = centralDirectoryRecords.Last();
            metadata.EndOfCentralDirectory = lastCentralDirectoryRecord.Position + lastCentralDirectoryRecord.HeaderSize;
            metadata.CentralDirectoryHeaders = centralDirectoryRecords;

            // Update central directory records by excluding the signature entry
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
                    SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(CentralDirectoryHeaderSignature));
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
        /// Throws SignatureException if less or more entries are found.
        /// </summary>
        /// <param name="metadata">SignedPackageArchiveMetadata to be checked for signature entry.</param>
        public static void AssertExactlyOnePrimarySignature(SignedPackageArchiveMetadata metadata)
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

        public static long WriteFileHeader(BinaryWriter writer, byte[] fileData, DateTime fileTime)
        {
            if (fileData == null || fileData.Length == 0)
            {
                throw new ArgumentNullException(nameof(fileData));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // Write the file header signature
            writer.Write(LocalFileHeaderSignature);

            // 2.0 - File is compressed using Deflate compression - Version needed to extract
            writer.Write((ushort)20);

            // 00 - Normal (-en) compression option was used.
            writer.Write((ushort)0);

            // 00 - The file is stored (no compression)
            writer.Write((ushort)0);

            // write date and time
            writer.Write(ToMsDosDateTime(fileTime));

            // write file CRC32
            writer.Write(GenerateCRC32(fileData));

            // write uncompressed size
            writer.Write((uint)fileData.Length);

            // write compressed size - same as uncompressed since file should have no compression
            writer.Write((uint)fileData.Length);

            // write file name length
            var fileNameBytes = _signingSpecification.Encoding.GetBytes(_signingSpecification.SignaturePath);
            var fileNameLength = fileNameBytes.Length;
            writer.Write((ushort)fileNameLength);

            // write extra field length
            writer.Write((ushort)0);

            // write file name
            writer.Write(fileNameBytes);

            // calculate the total length of data written
            // 30 bytes are for the length of the local file header
            var writtenDataLength = 30L + (ushort)fileNameLength;

            return writtenDataLength;
        }

        public static long WriteFile(BinaryWriter writer, byte[] fileData)
        {
            if (fileData == null || fileData.Length == 0)
            {
                throw new ArgumentNullException(nameof(fileData));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // write file
            writer.Write(fileData);

            // calculate the total length of data written
            var writtenDataLength = (long)fileData.Length;

            return writtenDataLength;
        }

        public static long WriteCentralDirectoryHeader(BinaryWriter writer, byte[] fileData, DateTime fileTime, long fileOffset)
        {
            if (fileData == null || fileData.Length == 0)
            {
                throw new ArgumentNullException(nameof(fileData));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // Write the file header signature
            writer.Write(CentralDirectoryHeaderSignature);

            // 2.0 - File is compressed using Deflate compression - Version made by
            writer.Write((ushort)20);

            // 2.0 - File is compressed using Deflate compression - Version needed to extract
            writer.Write((ushort)20);

            // 00 - Normal (-en) compression option was used.
            writer.Write((ushort)0);

            // 00 - The file is stored (no compression)
            writer.Write((ushort)0);

            // write date and time
            writer.Write(ToMsDosDateTime(fileTime));

            // write file CRC32
            writer.Write(GenerateCRC32(fileData));

            // write uncompressed size
            writer.Write(fileData.Length);

            // write compressed size - same as uncompressed since file should have no compression
            writer.Write(fileData.Length);

            // write file name length
            var fileNameBytes = _signingSpecification.Encoding.GetBytes(_signingSpecification.SignaturePath);
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
            // 50 bytes are for the length of the central directory header
            var writtenDataLength = 50L + (ushort)fileNameLength;

            return writtenDataLength;
        }

        public static void WriteEndOfCentralDirectoryRecord(BinaryReader reader, BinaryWriter writer, long sizeOfSignatureCentralDirectoryRecord, long sizeOfSignatureFileHeaderAndData)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

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

            var offsetOfCentralDirectory = reader.ReadUInt32();
            writer.Write((uint)(offsetOfCentralDirectory + sizeOfSignatureFileHeaderAndData));

            // read and write the rest
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

        // Reference - http://referencesource.microsoft.com/#WindowsBase/Base/MS/Internal/IO/Zip/ZipIOBlockManager.cs,492
        private static uint ToMsDosDateTime(DateTime dateTime)
        {

            uint result = 0;

            result |= (((uint)dateTime.Second) / 2) & 0x1F;   // seconds need to be divided by 2 as they stored in 5 bits
            result |= (((uint)dateTime.Minute) & 0x3F) << 5;
            result |= (((uint)dateTime.Hour) & 0x1F) << 11;

            result |= (((uint)dateTime.Day) & 0x1F) << 16;
            result |= (((uint)dateTime.Month) & 0xF) << 21;
            result |= (((uint)(dateTime.Year - 1980)) & 0x7F) << 25;

            return result;
        }

        // Reference - http://referencesource.microsoft.com/#WindowsBase/Base/MS/Internal/IO/Zip/Crc32.cs,73
        private static uint GenerateCRC32(byte[] data)
        {
            var crcTable = GenerateCrc32LookupTable();
            var crc32 = 0xffffffff;

            foreach (var dataByte in data)
            {
                var index = (crc32 ^ dataByte) & 0x000000FF;
                crc32 = (crc32 >> 8) ^ crcTable[index];
            }

            return ~crc32;
        }

        private static uint[] GenerateCrc32LookupTable()
        {
            var crcTable = new uint[256];

            for (var i = 0; i < crcTable.Length; i++)
            {
                var c = (uint)i;
                for (var j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0)
                        c = 0xedb88320 ^ (c >> 1);
                    else
                        c >>= 1;
                }
                crcTable[i] = c;
            }

            return crcTable;
        }
    }
}
