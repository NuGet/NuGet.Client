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

                // Jump to offset of start of central directory
                reader.BaseStream.Seek(offset: 16, origin: SeekOrigin.Current);
                var offsetOfStartOfCD = reader.ReadUInt32();

                // Look for signature central directory record
                reader.BaseStream.Seek(offset: offsetOfStartOfCD, origin: SeekOrigin.Begin);

                var ReadingCentralDirectoryHeaders = true;
                while (ReadingCentralDirectoryHeaders)
                {
                    // Skip until file name length
                    reader.BaseStream.Seek(offset: 28, origin: SeekOrigin.Current);
                    var filenameLength = reader.ReadUInt16();

                    // Skip to read filename
                    reader.BaseStream.Seek(offset: 16, origin: SeekOrigin.Current);

                    var filename = reader.ReadBytes(filenameLength);
                    var filenameString = Encoding.UTF8.GetString(filename);
                    if (string.Equals(filenameString, _signingSpecification.SignaturePath, StringComparison.Ordinal))
                    {
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
        /// Verifies that a signed zip archive's signature is valid and it has not been tampered with.
        /// </summary>
        /// <param name="reader">Signed zip archive to verify</param>
        /// <param name="verificationProvider">Provider for validations</param>
        /// <returns>True if package archive's hash matches the expected hash</returns>
        public static bool VerifySignedZipIntegrity(BinaryReader reader, HashAlgorithm hashAlgorithm, byte[] expectedHash)
        {
            // Make sure it is signed with a valid signature file
            if (!IsSigned(reader))
            {
                throw new Exception(Strings.SignedPackageNotSignedOnVerify);
            }

            try
            {
                var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

                reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.SignatureLocalFileHeaderPosition);

                // Skip hashing of signature file
                reader.BaseStream.Seek(offset: metadata.SignatureFileEntryTotalSize, origin: SeekOrigin.Current);

                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.SignatureCentralDirectoryHeaderPosition);

                // Skip hashing of central directory for signature file
                reader.BaseStream.Seek(offset: metadata.SignatureCentralDirectoryEntrySize, origin: SeekOrigin.Current);

                // Update offset of any central directory that comes after signature
                var possibleCentralDirectoryHeaderSignature = reader.ReadUInt32();
                while (possibleCentralDirectoryHeaderSignature == SignedPackageArchiveIOUtility.CentralDirectoryHeaderSignature)
                {
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(possibleCentralDirectoryHeaderSignature));
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Position + 24);

                    var filenameLength = reader.ReadUInt16();
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(filenameLength));

                    var extraFieldLength = reader.ReadUInt16();
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(extraFieldLength));

                    var fileCommentLength = reader.ReadUInt16();
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(fileCommentLength));

                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Position + 8);

                    var relativeOffset = (uint)(reader.ReadUInt32() - metadata.SignatureFileEntryTotalSize);
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(relativeOffset));

                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm,
                        reader.BaseStream.Position + filenameLength + extraFieldLength + fileCommentLength);

                    possibleCentralDirectoryHeaderSignature = reader.ReadUInt32();
                }

                // Seek back the last 4 bytes we read as a possible signature
                reader.BaseStream.Seek(offset: -4, origin: SeekOrigin.Current);

                // Update zip64 data
                if (metadata.IsZip64)
                {
                    SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.Zip64EndOfCentralDirectoryRecordPosition + 24);

                    var entryCountInDisk = (ulong)(reader.ReadUInt64() - 1);
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(entryCountInDisk));

                    var entryCount = (ulong)(reader.ReadUInt64() -1);
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(entryCount));

                    var sizeOfCentralDirectory = reader.ReadUInt64() -(ulong)metadata.SignatureCentralDirectoryEntrySize;
                    SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(sizeOfCentralDirectory));
                }

                // Update EOCD data
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, metadata.EndOfCentralDirectoryRecordPosition + 8);

                var eocdrTotalEntries = (ushort)(reader.ReadUInt16() - 1);
                var eocdrTotalEntriesOnDisk = (ushort)(reader.ReadUInt16() - 1);

                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntries));
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrTotalEntriesOnDisk));

                var eocdrSizeOfCentralDirectory = reader.ReadUInt32() - (uint)metadata.SignatureCentralDirectoryEntrySize;
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrSizeOfCentralDirectory));

                var eocdrOffsetOfCentralDirectory = reader.ReadUInt32() - (uint)metadata.SignatureFileEntryTotalSize;
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, BitConverter.GetBytes(eocdrOffsetOfCentralDirectory));

                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(reader, hashAlgorithm, reader.BaseStream.Length);

                hashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                return CompareHash(expectedHash, hashAlgorithm.Hash);
            }
            // If exception is throw in means the archive was not a valid package. It has been tampered, return false.
            catch { }

            return false;
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
