// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Zip Spec here: http://www.pkware.com/documents/casestudies/APPNOTE.TXT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Packaging.Signing
{
    internal static class SignedPackageArchiveUtility
    {
        private static readonly SigningSpecifications _signingSpecification = SigningSpecifications.V1;

#if IS_DESKTOP
        /// <summary>
        /// Utility method to know if a zip archive is signed.
        /// </summary>
        /// <param name="reader">Binary reader pointing to a zip archive.</param>
        /// <returns>true if the given archive is signed</returns>
        private static bool IsSigned(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            try
            {
                // Look for EOCD signature, typically is around 22 bytes from the end
                reader.BaseStream.Seek(offset: -22, origin: SeekOrigin.End);
                SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(reader,
                    BitConverter.GetBytes(SignedPackageArchiveIOUtility.EndOfCentralDirectorySignature));

                // Jump to offset of start of central directory, 16 bytes from the start of EOCD
                reader.BaseStream.Seek(offset: 16, origin: SeekOrigin.Current);
                var offsetOfStartOfCD = reader.ReadUInt32();

                // Look for signature central directory record
                reader.BaseStream.Seek(offset: offsetOfStartOfCD, origin: SeekOrigin.Begin);

                var ReadingCentralDirectoryHeaders = true;
                while (ReadingCentralDirectoryHeaders)
                {
                    var centralDirectoryHeaderSignature = reader.ReadUInt32();
                    if (centralDirectoryHeaderSignature != SignedPackageArchiveIOUtility.CentralDirectoryHeaderSignature)
                    {
                        throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
                    }

                    // Skip until file name length, 24 bytes after signature of central directory record
                    reader.BaseStream.Seek(offset: 24, origin: SeekOrigin.Current);
                    var filenameLength = reader.ReadUInt16();

                    // Skip to read local header offset (12 bytes after filename length field)
                    reader.BaseStream.Seek(offset: 12, origin: SeekOrigin.Current);

                    var localHeaderOffset = reader.ReadUInt32();

                    var filename = reader.ReadBytes(filenameLength);
                    var filenameString = Encoding.ASCII.GetString(filename);
                    if (string.Equals(filenameString, _signingSpecification.SignaturePath, StringComparison.Ordinal))
                    {
                        // Go to local file header
                        reader.BaseStream.Seek(offset: localHeaderOffset, origin: SeekOrigin.Begin);

                        // Make sure file header exists there
                        var fileHeaderSignature = reader.ReadUInt32();
                        if (fileHeaderSignature != SignedPackageArchiveIOUtility.LocalFileHeaderSignature)
                        {
                            throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
                        }

                        return true;
                    }

                    SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(reader,
                        BitConverter.GetBytes(SignedPackageArchiveIOUtility.CentralDirectoryHeaderSignature));
                }
            }
            // Ignore any exception. If something is thrown it means the archive is either not valid or not signed
            catch { }

            return false;
        }

        /// <summary>
        /// Signs a Zip with the contents in the SignatureStream using the writer.
        /// The reader is used to read the exisiting contents for the Zip.
        /// </summary>
        /// <param name="signatureStream">MemoryStream of the signature to be inserted into the zip.</param>
        /// <param name="reader">BinaryReader to be used to read the existing zip data.</param>
        /// <param name="writer">BinaryWriter to be used to write the signature into the zip.</param>
        public static void SignZip(MemoryStream signatureStream, BinaryReader reader, BinaryWriter writer)
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

            SignedPackageArchiveIOUtility.WriteSignatureIntoZip(signatureStream, reader, writer);
        }

        /// <summary>
        /// Verifies that a signed package archive's signature is valid and it has not been tampered with.
        /// </summary>
        /// <param name="reader">Signed zip archive to verify</param>
        /// <param name="hashAlgorithm">Hash algorithm to be used to hash data.</param>
        /// <param name="expectedHash">Hash value of the original data.</param>
        /// <returns>True if package archive's hash matches the expected hash</returns>
        public static bool VerifySignedZipIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            // Make sure it is signed with a valid signature file
            if (!IsSigned(reader))
            {
                throw new Exception(Strings.SignedPackageNotSignedOnVerify);
            }

            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

            // Assert exactly one primary signature
            SignedPackageArchiveIOUtility.AssertExactlyOnePrimarySignature(metadata);

            var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];
            var centralDirectoryRecordsWithoutSignature = RemoveSignatureAndOrderByOffset(metadata);

            try
            {
                // Read and hash from the start of the archive to the start of the file headers
                reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.StartOfFileHeaders);

                // Read and hash file headers
                foreach (var record in centralDirectoryRecordsWithoutSignature)
                {
                    reader.BaseStream.Seek(offset: record.OffsetToFileHeader, origin: SeekOrigin.Begin);
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, record.OffsetToFileHeader + record.FileEntryTotalSize);
                }

                // Order central directory records by their position
                centralDirectoryRecordsWithoutSignature.Sort((x, y) => x.Position.CompareTo(y.Position));

                // Update offset of any central directory record that has a file entry after signature
                foreach (var record in centralDirectoryRecordsWithoutSignature)
                {
                    reader.BaseStream.Seek(offset: record.Position, origin: SeekOrigin.Begin);
                    // Hash from the start of the central directory record until the relative offset of local file header (42 from the start of central directory record, including signature length)
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Position + 42);

                    var relativeOffsetOfLocalFileHeader = (uint)(reader.ReadUInt32() + record.ChangeInOffset);
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(relativeOffsetOfLocalFileHeader));

                    // We already read and hash the whole header, skip only filenameLength + extraFieldLength + fileCommentLength
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Position + record.HeaderSize - SignedPackageArchiveIOUtility.CentralDirectoryFileHeaderSize);
                }

                reader.BaseStream.Seek(offset: metadata.EndOfCentralDirectory, origin: SeekOrigin.Begin);

                // Hash until total entries in end of central directory record (8 bytes from the start of EOCDR)
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.EndOfCentralDirectoryRecordPosition + 8);

                var eocdrTotalEntries = (ushort)(reader.ReadUInt16() - 1);
                var eocdrTotalEntriesOnDisk = (ushort)(reader.ReadUInt16() - 1);

                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntries));
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntriesOnDisk));

                // update the central directory size by substracting the length of the signature header size and the central directory header signature
                var eocdrSizeOfCentralDirectory = (uint)(reader.ReadUInt32() - signatureCentralDirectoryHeader.HeaderSize - BitConverter.GetBytes(SignedPackageArchiveIOUtility.CentralDirectoryHeaderSignature).Length);
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrSizeOfCentralDirectory));

                var eocdrOffsetOfCentralDirectory = reader.ReadUInt32() - (uint)signatureCentralDirectoryHeader.FileEntryTotalSize;
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrOffsetOfCentralDirectory));

                // Hash until the end of the reader
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Length);

                hashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                return CompareHash(expectedHash, hashAlgorithm.Hash);
            }
            // If exception is throw in means the archive was not a valid package. It has been tampered, return false.
            catch { }

            return false;
        }

        private static List<CentralDirectoryHeaderMetadata> RemoveSignatureAndOrderByOffset(SignedPackageArchiveMetadata metadata)
        {
            // Remove signature cdr
            var centralDirectoryRecordsList = metadata.CentralDirectoryHeaders.Where((v, i) => i != metadata.SignatureCentralDirectoryHeaderIndex).ToList();

            // Sort by order of file entries
            centralDirectoryRecordsList.Sort((x, y) => x.OffsetToFileHeader.CompareTo(y.OffsetToFileHeader));

            // Update offsets with removed signature
            var previousRecordFileEntryEnd = 0L;
            foreach (var centralDirectoryRecord in centralDirectoryRecordsList)
            {
                centralDirectoryRecord.ChangeInOffset = previousRecordFileEntryEnd - centralDirectoryRecord.OffsetToFileHeader;

                previousRecordFileEntryEnd = centralDirectoryRecord.OffsetToFileHeader + centralDirectoryRecord.FileEntryTotalSize + centralDirectoryRecord.ChangeInOffset;
            }

            return centralDirectoryRecordsList;
        }

#else

        public static bool IsSigned(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public static void SignZip(MemoryStream signatureStream, BinaryReader reader, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public static void VerifySignedZipIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            throw new NotImplementedException();
        }

#endif

        private static bool CompareHash(byte[] expectedHash, byte[] actualHash)
        {
            if (expectedHash.Length != actualHash.Length)
            {
                return false;
            }

            for (var i = 0; i < expectedHash.Length; i++)
            {
                if (expectedHash[i] != actualHash[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
