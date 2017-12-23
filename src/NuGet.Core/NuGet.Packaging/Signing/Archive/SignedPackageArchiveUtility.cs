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
    /// <remarks>This is public only to facilitate testing.</remarks>
    public static class SignedPackageArchiveUtility
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
            try
            {
                var endOfCentralDirectoryRecord = EndOfCentralDirectoryRecord.Read(reader);

                // Look for signature central directory record
                reader.BaseStream.Seek(endOfCentralDirectoryRecord.OffsetOfStartOfCentralDirectory, SeekOrigin.Begin);
                CentralDirectoryHeader header;

                while (CentralDirectoryHeader.TryRead(reader, out header))
                {
                    var isUtf8 = IsUtf8(header.GeneralPurposeBitFlag);
                    var fileName = GetString(header.FileName, isUtf8);

                    if (string.Equals(fileName, _signingSpecification.SignaturePath, StringComparison.Ordinal))
                    {
                        // Go to local file header
                        reader.BaseStream.Seek(header.RelativeOffsetOfLocalHeader, SeekOrigin.Begin);

                        // Make sure file header exists there
                        var fileHeaderSignature = reader.ReadUInt32();
                        if (fileHeaderSignature != LocalFileHeader.Signature)
                        {
                            throw new InvalidDataException(Strings.ErrorInvalidPackageArchive);
                        }

                        return true;
                    }
                }
            }
            // Ignore any exception. If something is thrown it means the archive is either not valid or not signed
            catch { }

            return false;
        }
#endif

        public static bool IsZip64(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var endOfCentralDirectoryRecord = EndOfCentralDirectoryRecord.Read(reader);

            if (endOfCentralDirectoryRecord.NumberOfThisDisk != endOfCentralDirectoryRecord.NumberOfTheDiskWithTheStartOfTheCentralDirectory)
            {
                return false;
            }

            var offset = endOfCentralDirectoryRecord.OffsetFromStart - Zip64EndOfCentralDirectoryLocator.SizeInBytes;

            if (offset >= 0)
            {
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                if (Zip64EndOfCentralDirectoryLocator.Exists(reader))
                {
                    return true;
                }
            }

            reader.BaseStream.Seek(endOfCentralDirectoryRecord.OffsetOfStartOfCentralDirectory, SeekOrigin.Begin);

            CentralDirectoryHeader centralDirectoryHeader;

            while (CentralDirectoryHeader.TryRead(reader, out centralDirectoryHeader))
            {
                if (HasZip64ExtendedInformationExtraField(centralDirectoryHeader))
                {
                    return true;
                }

                if (centralDirectoryHeader.DiskNumberStart != endOfCentralDirectoryRecord.NumberOfThisDisk)
                {
                    continue;
                }

                var savedPosition = reader.BaseStream.Position;

                reader.BaseStream.Position = centralDirectoryHeader.RelativeOffsetOfLocalHeader;

                LocalFileHeader localFileHeader;

                if (LocalFileHeader.TryRead(reader, out localFileHeader) &&
                    HasZip64ExtendedInformationExtraField(localFileHeader))
                {
                    return true;
                }

                reader.BaseStream.Position = savedPosition;
            }

            return false;
        }

        private static bool HasZip64ExtendedInformationExtraField(CentralDirectoryHeader header)
        {
            IReadOnlyList<ExtraField> extraFields;

            if (ExtraField.TryRead(header, out extraFields))
            {
                return extraFields.Any(extraField => extraField is Zip64ExtendedInformationExtraField);
            }

            return false;
        }

        private static bool HasZip64ExtendedInformationExtraField(LocalFileHeader header)
        {
            IReadOnlyList<ExtraField> extraFields;

            if (ExtraField.TryRead(header, out extraFields))
            {
                return extraFields.Any(extraField => extraField is Zip64ExtendedInformationExtraField);
            }

            return false;
        }

#if IS_DESKTOP
        /// <summary>
        /// Signs a Zip with the contents in the SignatureStream using the writer.
        /// The reader is used to read the exisiting contents for the Zip.
        /// </summary>
        /// <param name="signatureStream">MemoryStream of the signature to be inserted into the zip.</param>
        /// <param name="reader">BinaryReader to be used to read the existing zip data.</param>
        /// <param name="writer">BinaryWriter to be used to write the signature into the zip.</param>
        internal static void SignZip(MemoryStream signatureStream, BinaryReader reader, BinaryWriter writer)
        {
            SignedPackageArchiveIOUtility.WriteSignatureIntoZip(signatureStream, reader, writer);
        }

        /// <summary>
        /// Verifies that a signed package archive's signature is valid and it has not been tampered with.
        /// </summary>
        /// <param name="reader">Signed package to verify</param>
        /// <param name="hashAlgorithm">Hash algorithm to be used to hash data.</param>
        /// <param name="expectedHash">Hash value of the original data.</param>
        /// <returns>True if package archive's hash matches the expected hash</returns>
        internal static bool VerifySignedPackageIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (hashAlgorithm == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            if (expectedHash == null || expectedHash.Length == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(reader));
            }

            // Make sure it is signed with a valid signature file
            if (!IsSigned(reader))
            {
                throw new Exception(Strings.SignedPackageNotSignedOnVerify);
            }

            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

            // Assert exactly one primary signature
            SignedPackageArchiveIOUtility.AssertExactlyOnePrimarySignatureAndUpdateMetadata(metadata);

            var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

            // Assert signature entry metadata
            SignedPackageArchiveIOUtility.AssertSignatureEntryMetadata(reader, signatureCentralDirectoryHeader);

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

                    // Continue hashing file name, extra field, and file comment fields.
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Position + record.HeaderSize - CentralDirectoryHeader.SizeInBytesOfFixedLengthFields);
                }

                reader.BaseStream.Seek(offset: metadata.EndOfCentralDirectory, origin: SeekOrigin.Begin);

                // Hash until total entries in end of central directory record (8 bytes from the start of EOCDR)
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.EndOfCentralDirectoryRecordPosition + 8);

                var eocdrTotalEntries = (ushort)(reader.ReadUInt16() - 1);
                var eocdrTotalEntriesOnDisk = (ushort)(reader.ReadUInt16() - 1);

                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntries));
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntriesOnDisk));

                // update the central directory size by substracting the size of the package signature file's central directory header
                var eocdrSizeOfCentralDirectory = (uint)(reader.ReadUInt32() - signatureCentralDirectoryHeader.HeaderSize);
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

        internal static bool IsSigned(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        internal static void SignZip(MemoryStream signatureStream, BinaryReader reader, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        internal static void VerifySignedZipIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            throw new NotImplementedException();
        }

#endif
        internal static bool IsUtf8(uint generalPurposeBitFlags)
        {
            return (generalPurposeBitFlags & (1 << 11)) == 1;
        }

        internal static string GetString(byte[] bytes, bool isUtf8)
        {
            if (isUtf8)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return Encoding.ASCII.GetString(bytes);
        }

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