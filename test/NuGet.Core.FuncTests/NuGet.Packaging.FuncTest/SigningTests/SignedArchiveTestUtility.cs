// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    internal static class SignedArchiveTestUtility
    {
        // Central Directory file header size excluding signature, file name, extra field and file comment
        private const uint CentralDirectoryFileHeaderSizeWithoutSignature = 46;
        private const string _internalTimestampServer = "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer";

        /// <summary>
        /// Generates a signed copy of a package and returns the path to that package
        /// </summary>
        /// <param name="testCert">Certificate to be used while signing the package</param>
        /// <param name="nupkg">Package to be signed</param>
        /// <param name="dir">Directory for placing the signed package</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedPackageAsync(X509Certificate2 testCert, SimpleTestPackageContext nupkg, string dir)
        {
            var testLogger = new TestLogger();

            using (var zipReadStream = nupkg.CreateAsStream())
            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipReadStream, zipWriteStream))
                {
                    // Sign the package
                    await SignPackageAsync(testLogger, testCert, signPackage);
                }

                zipWriteStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(signedPackagePath))
                {
                    zipWriteStream.CopyTo(fileStream);
                }

                return signedPackagePath;
            }
        }

        /// <summary>
        /// Generates a signed copy of a package and returns the path to that package
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        /// <param name="testCert">Certificate to be used while signing the package</param>
        /// <param name="nupkg">Package to be signed</param>
        /// <param name="dir">Directory for placing the signed package</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedAndTimeStampedPackageAsync(X509Certificate2 testCert, SimpleTestPackageContext nupkg, string dir)
        {
            var testLogger = new TestLogger();

            using (var zipReadStream = nupkg.CreateAsStream())
            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipReadStream, zipWriteStream))
                {
                    // Sign the package
                    await SignAndTimeStampPackageAsync(testLogger, testCert, signPackage);
                }

                zipWriteStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(signedPackagePath))
                {
                    zipWriteStream.CopyTo(fileStream);
                }

                return signedPackagePath;
            }
        }

        /// <summary>
        /// unsigns a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        public static async Task UnsignPackageAsync(string signedPackagePath, string dir)
        {
            var testLogger = new TestLogger();
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);

            var copiedSignedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());
            File.Copy(signedPackagePath, copiedSignedPackagePath, overwrite: true);

            using (var zipReadStream = File.Open(signedPackagePath, FileMode.Open))
            using (var zipWriteStream = File.Open(copiedSignedPackagePath, FileMode.Open))
            using (var signedPackage = new SignedPackageArchive(zipReadStream, zipWriteStream))
            {
                var signer = new Signer(signedPackage, testSignatureProvider);
                await signer.RemoveSignaturesAsync(testLogger, CancellationToken.None);
            }

            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
        }

        /// <summary>
        /// Generates a Signature for a package.
        /// </summary>
        /// <param name="testCert">Certificate to be used while generating the signature.</param>
        /// <param name="nupkg">Package for which the signature has to be generated.</param>
        /// <returns>Signature for the package.</returns>
        public static async Task<Signature> CreateSignatureForPackageAsync(X509Certificate2 testCert, Stream packageStream)
        {
            var testLogger = new TestLogger();
            var hashAlgorithm = HashAlgorithmName.SHA256;

            using (var request = new SignPackageRequest() { Certificate = testCert, SignatureHashAlgorithm = hashAlgorithm })
            using (var package = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var zipArchiveHash = await package.GetArchiveHashAsync(request.SignatureHashAlgorithm, CancellationToken.None);
                var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);
                var signatureContent = new SignatureContent(hashAlgorithm, base64ZipArchiveHash);
                var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);

                return await testSignatureProvider.CreateSignatureAsync(request, signatureContent, testLogger, CancellationToken.None);
            }
        }

        /// <summary>
        /// Sign a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        private static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                SignatureHashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }

        /// <summary>
        /// Sign and timestamp a package for test purposes.
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        private static async Task SignAndTimeStampPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(new Rfc3161TimestampProvider(new Uri(_internalTimestampServer)));
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                SignatureHashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }


        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new SignatureTrustAndValidityVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders, settings);
            var result = await verifier.VerifySignaturesAsync(signPackage, CancellationToken.None);
            return result;
        }

        /// <summary>
        /// unsigns a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        public static async Task ShiftSignatureMetadataAsync(SigningSpecifications spec, string signedPackagePath, string dir, int centralDirectoryIndex, int fileHeaderIndex)
        {
            var testLogger = new TestLogger();
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);

            // Create a temp path
            var copiedSignedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

            using (var signedReadStream = File.OpenRead(signedPackagePath))
            using (var signedPackage = new BinaryReader(signedReadStream))
            using (var shiftedWriteStream = File.OpenWrite(copiedSignedPackagePath))
            using (var shiftedPackage = new BinaryWriter(shiftedWriteStream))
            {
                await ShiftSignatureMetadata(spec, signedPackage, shiftedPackage, centralDirectoryIndex, fileHeaderIndex);
            }

            // Overwrite the original package with the shifted one
            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
        }

        public static int GetSignatureCentralDirectoryIndex(SignedPackageArchiveMetadata metadata, SigningSpecifications signingSpecification)
        {
            var centralDirectoryRecords = metadata.CentralDirectoryHeaders;
            var centralDirectoryRecordsCount = centralDirectoryRecords.Count;

            for (var centralDirectoryRecordIndex = 0; centralDirectoryRecordIndex < centralDirectoryRecordsCount; centralDirectoryRecordIndex++)
            {
                var record = centralDirectoryRecords[centralDirectoryRecordIndex];

                if (StringComparer.Ordinal.Equals(record.Filename, signingSpecification.SignaturePath))
                {
                    return centralDirectoryRecordIndex;
                }
            }

            return -1;
        }

        private static Task ShiftSignatureMetadata(SigningSpecifications spec, BinaryReader reader, BinaryWriter writer, int centralDirectoryIndex, int fileHeaderIndex)
        {
            // Read metadata
            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

            // Update central directory records by excluding the signature entry
            SignedPackageArchiveIOUtility.UpdateSignedPackageArchiveMetadata(reader, metadata);

            // Calculate new central directory record metadata with the the signature record and entry shifted
            var shiftedCdr = ShiftMetadata(spec, metadata, newSignatureFileEntryIndex: fileHeaderIndex, newSignatureCentralDirectoryRecordIndex: centralDirectoryIndex);

            // Order records by shifted ofset (new offset = old offset + change in offset).
            // This is the order they will appear in the new shifted package, but not necesarily the same order they were in the old package
            shiftedCdr.Sort((x, y) => (x.OffsetToFileHeader + x.ChangeInOffset).CompareTo(y.OffsetToFileHeader + y.ChangeInOffset));

            // Write data from start of file to first file entry
            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, metadata.StartOfFileHeaders);

            // Write all file entries in the new order
            foreach (var entry in shiftedCdr)
            {
                // We need to read each entry from their position in the old package and write them sequencially to the new package
                // The order in which they will appear in the new shited package is defined by the sorting done before starting to write
                reader.BaseStream.Seek(offset: entry.OffsetToFileHeader, origin: SeekOrigin.Begin);
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, entry.OffsetToFileHeader + entry.FileEntryTotalSize);
            }

            // Write all central directory records with updated offsets
            // We first need to sort them in the order they will appear in the new shifted package
            shiftedCdr.Sort((x, y) => x.IndexInHeaders.CompareTo(y.IndexInHeaders));
            foreach (var entry in shiftedCdr)
            {
                reader.BaseStream.Seek(offset: entry.Position, origin: SeekOrigin.Begin);
                // Read and write from the start of the central directory record until the relative offset of local file header (42 from the start of central directory record, incluing signature length)
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + 42);

                var relativeOffsetOfLocalFileHeader = (uint)(reader.ReadUInt32() + entry.ChangeInOffset);
                writer.Write(relativeOffsetOfLocalFileHeader);

                // We already read and hash the whole header, skip only filenameLength + extraFieldLength + fileCommentLength (46 is the size of the header without those lengths)
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + entry.HeaderSize - CentralDirectoryFileHeaderSizeWithoutSignature);
            }

            // Write everything after central directory records
            reader.BaseStream.Seek(offset: metadata.EndOfCentralDirectory, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Length);

            return Task.FromResult(0);
        }

        private static List<CentralDirectoryHeaderMetadata> ShiftMetadata(
            SigningSpecifications spec,
            SignedPackageArchiveMetadata metadata,
            int newSignatureFileEntryIndex,
            int newSignatureCentralDirectoryRecordIndex)
        {
            var shiftedCdr = new List<CentralDirectoryHeaderMetadata>(metadata.CentralDirectoryHeaders);

            // Sort Central Directory records in the order the file entries appear  in the original archive
            shiftedCdr.Sort((x, y) => x.OffsetToFileHeader.CompareTo(y.OffsetToFileHeader));

            // Shift Central Directory records to the desired position.
            // Because we sorted in the file entry order this will shift
            // the file entries
            ShiftSignatureToIndex(spec, shiftedCdr, newSignatureFileEntryIndex);

            // Calculate the change in offsets for the shifted file entries
            var lastEntryEnd = 0L;
            foreach (var cdr in shiftedCdr)
            {
                cdr.ChangeInOffset = lastEntryEnd - cdr.OffsetToFileHeader;

                lastEntryEnd = cdr.OffsetToFileHeader + cdr.FileEntryTotalSize + cdr.ChangeInOffset;
            }

            // Now we sort the central directory records in the order thecentral directory records appear in the original archive
            shiftedCdr.Sort((x, y) => x.Position.CompareTo(y.Position));

            // Shift Central Directory records to the desired position.
            // Because we sorted in the central directory records order this will shift
            // the central directory records
            ShiftSignatureToIndex(spec, shiftedCdr, newSignatureCentralDirectoryRecordIndex);

            // Calculate the new indexes for each central directory record
            var lastIndex = 0;
            foreach (var cdr in shiftedCdr)
            {
                cdr.IndexInHeaders = lastIndex;
                lastIndex++;
            }

            return shiftedCdr;
        }

        private static void ShiftSignatureToIndex(
            SigningSpecifications spec,
            List<CentralDirectoryHeaderMetadata> cdr,
            int index)
        {
            // Check for the signature object in the entries.
            // We have to do a manual check because we have no context
            // of the order in which the central directory records list is sorted.
            var recordIndex = 0;
            for (; recordIndex < cdr.Count; recordIndex++)
            {
                if (StringComparer.Ordinal.Equals(cdr[recordIndex].Filename, spec.SignaturePath))
                {
                    break;
                }
            }
            // Remove the signature object and add it to the new index
            var signatureCD = cdr[recordIndex];
            cdr.RemoveAt(recordIndex);
            cdr.Insert(index, signatureCD);
        }
    }
}