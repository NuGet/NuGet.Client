// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Packaging.Signing
{
    internal static class SignedPackageArchiveUtility
    {

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

            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(reader, BitConverter.GetBytes(SignedPackageArchiveIOUtility.LocalFileHeaderSignature));

            // Read local file headers until signature is found
            var possibleSignature = reader.ReadUInt32();
            while (possibleSignature == SignedPackageArchiveIOUtility.LocalFileHeaderSignature)
            {
                // Jump to file name length
                reader.BaseStream.Seek(offset: 14, origin: SeekOrigin.Current);

                var compressedSize = reader.ReadUInt32();
                reader.BaseStream.Seek(offset: 4, origin: SeekOrigin.Current);

                var filenameLength = reader.ReadUInt16();
                var extraFieldlength = reader.ReadUInt16();

                var filename = reader.ReadBytes(filenameLength);
                var filenameString = Encoding.UTF8.GetString(filename);
                if (string.Equals(filenameString, SignedPackageArchiveIOUtility.SignatureFilename))
                {
                    return true;
                }

                // Skip extra field and data
                reader.BaseStream.Seek(offset: extraFieldlength + compressedSize, origin: SeekOrigin.Current);

                possibleSignature = reader.ReadUInt32();
            }
            return false;
        }

        /// <summary>
        /// Verifies that a signed zip archive's signature is valid and it has not been tampered with.
        /// </summary>
        /// <param name="reader">Signed zip archive to verify</param>
        /// <param name="verificationProvider">Provider for validations</param>
        public static void VerifySignedZipIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            // Make sure it is signed with a valid signature file
            if (!IsSigned(reader))
            {
                throw new Exception("Zip archive is not signed.");
            }

            var zipMetadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
            var signatureFileCentralDirectoryRecordHeaderSize = 0L;

            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, zipMetadata.SignatureLocalFileHeaderPosition);

            // Skip hashing of signature file
            var extraEntrySize = 30 + zipMetadata.SignatureFileCompressedSize + zipMetadata.FileHeaderExtraSize;
            reader.BaseStream.Seek(offset: extraEntrySize, origin: SeekOrigin.Current);

            if (zipMetadata.SignatureHasDataDescriptor)
            {
                reader.BaseStream.Seek(offset: 12, origin: SeekOrigin.Current);
            }
            SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, zipMetadata.SignatureCentralDirectoryHeaderPosition);

            // skip until file name length
            reader.BaseStream.Seek(offset: 28, origin: SeekOrigin.Current);

            var filenameLength = reader.ReadUInt16();
            var extraFieldLength = reader.ReadUInt16();
            var fileCommentLength = reader.ReadUInt16();

            signatureFileCentralDirectoryRecordHeaderSize = 46 + filenameLength + extraFieldLength + fileCommentLength;
            reader.BaseStream.Seek(offset: 12 + filenameLength + extraFieldLength + fileCommentLength, origin: SeekOrigin.Current);

            if (zipMetadata.IsZip64)
            {
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, zipMetadata.Zip64EndOfCentralDirectoryRecordPosition + 16);

                var numberOfThisDisk = reader.ReadUInt32();
                var numberOfDisks = reader.ReadInt32();

                if (numberOfThisDisk != 0 || numberOfDisks != 0)
                {
                    throw new Exception("Disk number not supported");
                }

                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(numberOfThisDisk));
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(numberOfDisks));

                var entryCountInDisk = reader.ReadUInt64();
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(entryCountInDisk - 1));

                var entryCount = reader.ReadUInt64();
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(entryCount - 1));

                var sizeOfCentralDirectory = reader.ReadUInt64();
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(sizeOfCentralDirectory - (ulong)signatureFileCentralDirectoryRecordHeaderSize));

                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, zipMetadata.Zip64EndOfCentralDirectoryLocatorPosition + 16);

                var totalNumberOfDisks = reader.ReadUInt32();

                if (totalNumberOfDisks != 0)
                {
                    throw new Exception("Total disk number not supported");
                }
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(totalNumberOfDisks));
            }

            SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, zipMetadata.EndOfCentralDirectoryRecordPosition + 4);

            var eocdrNumberOfDisks = reader.ReadUInt16();
            var eocdrDiskWithStart = reader.ReadUInt16();

            if (eocdrNumberOfDisks != 0 || eocdrDiskWithStart != 0)
            {
                throw new Exception("Disk number not supported");
            }

            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrNumberOfDisks));
            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrDiskWithStart));

            var eocdrTotalEntries = (ushort)(reader.ReadUInt16() - 1);
            var eocdrTotalEntriesOnDisk = (ushort)(reader.ReadUInt16() - 1);

            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntries));
            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntriesOnDisk));

            var eocdrSizeOfCentralDirectory = (uint)(reader.ReadUInt32() - (uint)signatureFileCentralDirectoryRecordHeaderSize);
            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrSizeOfCentralDirectory));

            var eocdrOffsetOfCentralDirectory = (uint)(reader.ReadUInt32() - (uint)extraEntrySize);
            SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrOffsetOfCentralDirectory));

            SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Length);

            hashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

            if (!CompareHash(expectedHash, hashAlgorithm.Hash))
            {
                throw new Exception("Zip contents do match signature.");
            }
        }

#else

        public static bool IsSigned(BinaryReader reader)
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
