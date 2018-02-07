// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace Test.Utility.Signing
{
    public static class SignedArchiveTestUtility
    {
        // Central Directory file header size excluding signature, file name, extra field and file comment
        private const uint CentralDirectoryFileHeaderSizeWithoutSignature = 46;

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
        /// <param name="certificate">Certificate to be used while signing the package</param>
        /// <param name="nupkg">Package to be signed</param>
        /// <param name="dir">Directory for placing the signed package</param>
        /// <param name="timestampService">RFC 3161 timestamp service URL.</param>
        /// <param name="request">An author signing request.</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedAndTimeStampedPackageAsync(
            X509Certificate2 certificate,
            SimpleTestPackageContext nupkg,
            string dir,
            Uri timestampService,
            SignPackageRequest request = null)
        {
            var testLogger = new TestLogger();

            using (var zipReadStream = nupkg.CreateAsStream())
            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipReadStream, zipWriteStream))
                {
                    request = request ?? new SignPackageRequest(certificate, HashAlgorithmName.SHA256);

                    // Sign the package
                    await SignAndTimeStampPackageAsync(testLogger, certificate, signPackage, timestampService, request);
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
        /// Generates a Signature for a package.
        /// </summary>
        /// <param name="testCert">Certificate to be used while generating the signature.</param>
        /// <param name="nupkg">Package for which the signature has to be generated.</param>
        /// <returns>Signature for the package.</returns>
        public static async Task<Signature> CreateSignatureForPackageAsync(X509Certificate2 testCert, Stream packageStream, ITimestampProvider timestampProvider = null)
        {
            var testLogger = new TestLogger();
            var hashAlgorithm = HashAlgorithmName.SHA256;

            using (var request = new SignPackageRequest(testCert, hashAlgorithm))
            using (var package = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var zipArchiveHash = await package.GetArchiveHashAsync(request.SignatureHashAlgorithm, CancellationToken.None);
                var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);
                var signatureContent = new SignatureContent(SigningSpecifications.V1, hashAlgorithm, base64ZipArchiveHash);
                var testSignatureProvider = new X509SignatureProvider(timestampProvider);

                return await testSignatureProvider.CreateSignatureAsync(request, signatureContent, testLogger, CancellationToken.None);
            }
        }

        /// <summary>
        /// Sign a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        private static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 certificate, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);
            var signer = new Signer(signPackage, testSignatureProvider);
            var request = new SignPackageRequest(certificate, signatureHashAlgorithm: HashAlgorithmName.SHA256);

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }

        /// <summary>
        /// Sign and timestamp a package for test purposes.
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        private static async Task SignAndTimeStampPackageAsync(
            TestLogger testLogger,
            X509Certificate2 certificate,
            SignedPackageArchive signPackage,
            Uri timestampService,
            SignPackageRequest request)
        {
            var testSignatureProvider = new X509SignatureProvider(new Rfc3161TimestampProvider(timestampService));
            var signer = new Signer(signPackage, testSignatureProvider);

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
        /// Generates a Signature for a given package for tests.
        /// </summary>
        /// <param name="signatureProvider">Signature proivider to create the signature.</param>
        /// <param name="package">Package to be used for the signature.</param>
        /// <param name="request">SignPackageRequest containing the metadata for the signature request.</param>
        /// <param name="testLogger">ILogger.</param>
        /// <returns>Signature for the package.</returns>
        public static async Task<Signature> CreateSignatureForPackageAsync(ISignatureProvider signatureProvider, PackageArchiveReader package, SignPackageRequest request, TestLogger testLogger)
        {
            var zipArchiveHash = await package.GetArchiveHashAsync(request.SignatureHashAlgorithm, CancellationToken.None);
            var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);
            var signatureContent = new SignatureContent(SigningSpecifications.V1, request.SignatureHashAlgorithm, base64ZipArchiveHash);

            return await signatureProvider.CreateSignatureAsync(request, signatureContent, testLogger, CancellationToken.None);
        }

#if IS_DESKTOP
        private static SignatureContent CreateSignatureContent(FileInfo packageFile)
        {
            using (var stream = packageFile.OpenRead())
            using (var hashAlgorithm = HashAlgorithmName.SHA256.GetHashProvider())
            {
                var hash = hashAlgorithm.ComputeHash(stream, leaveStreamOpen: false);

                return new SignatureContent(SigningSpecifications.V1, HashAlgorithmName.SHA256, Convert.ToBase64String(hash));
            }
        }

        private static SignedCms CreateSignature(SignatureContent signatureContent, X509Certificate2 certificate)
        {
            var cmsSigner = new CmsSigner(certificate);

            cmsSigner.DigestAlgorithm = HashAlgorithmName.SHA256.ConvertToOid();
            cmsSigner.IncludeOption = X509IncludeOption.WholeChain;

            var contentInfo = new ContentInfo(signatureContent.GetBytes());
            var signedCms = new SignedCms(contentInfo);

            signedCms.ComputeSignature(cmsSigner);

            Assert.Empty(signedCms.SignerInfos[0].SignedAttributes);
            Assert.Empty(signedCms.SignerInfos[0].UnsignedAttributes);

            return signedCms;
        }

        // This generates a package with a basic signed CMS.
        // The signature MUST NOT have any signed or unsigned attributes.
        public static async Task<FileInfo> SignPackageFileWithBasicSignedCmsAsync(
            TestDirectory directory,
            FileInfo packageFile,
            X509Certificate2 certificate)
        {
            var signatureContent = CreateSignatureContent(packageFile);
            var signedPackageFile = new FileInfo(Path.Combine(directory, Guid.NewGuid().ToString()));
            var signature = CreateSignature(signatureContent, certificate);

            using (var packageReadStream = packageFile.OpenRead())
            using (var packageWriteStream = signedPackageFile.OpenWrite())
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            using (var signatureStream = new MemoryStream(signature.Encode()))
            {
                await package.AddSignatureAsync(signatureStream, CancellationToken.None);
            }

            return signedPackageFile;
        }
#endif

        /// <summary>
        /// Timestamps a signature for tests.
        /// </summary>
        /// <param name="timestampProvider">Timestamp provider.</param>
        /// <param name="signatureRequest">SignPackageRequest containing metadata for timestamp request./param>
        /// <param name="signature">Signature that needs to be timestamped.</param>
        /// <param name="logger">ILogger.</param>
        /// <returns>Timestamped Signature.</returns>
        public static Task<Signature> TimestampSignature(ITimestampProvider timestampProvider, SignPackageRequest signatureRequest, Signature signature, ILogger logger)
        {
            var timestampRequest = new TimestampRequest
            {
                SignatureValue = signature.GetBytes(),
                SigningSpec = SigningSpecifications.V1,
                TimestampHashAlgorithm = signatureRequest.TimestampHashAlgorithm
            };

            return TimestampSignature(timestampProvider, timestampRequest, signature, logger);
        }

        /// <summary>
        /// Timestamps a signature for tests.
        /// </summary>
        /// <param name="timestampProvider">Timestamp provider.</param>
        /// <param name="signature">Signature that needs to be timestamped.</param>
        /// <param name="logger">ILogger.</param>
        /// <param name="timestampRequest">timestampRequest containing metadata for timestamp request.</param>
        /// <returns>Timestamped Signature.</returns>
        public static Task<Signature> TimestampSignature(ITimestampProvider timestampProvider, TimestampRequest timestampRequest, Signature signature, ILogger logger)
        {
            return timestampProvider.TimestampSignatureAsync(timestampRequest, logger, CancellationToken.None);
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

                if (record.IsPackageSignatureFile)
                {
                    return centralDirectoryRecordIndex;
                }
            }

            return -1;
        }

#if IS_DESKTOP
        public static Signature GenerateInvalidSignature(Signature signature)
        {
            var hash = Encoding.UTF8.GetBytes(signature.SignatureContent.HashValue);
            var newHash = Encoding.UTF8.GetBytes(new string('0', hash.Length));

            var bytes = signature.SignedCms.Encode();
            var newBytes = FindAndReplaceSequence(bytes, hash, newHash);

            return Signature.Load(newBytes);
        }
#endif

        private static byte[] FindAndReplaceSequence(byte[] bytes, byte[] find, byte[] replace)
        {
            var found = false;
            var from = -1;

            for (var i = 0; !found && i < bytes.Length - find.Length; ++i)
            {
                for (var j = 0; j < find.Length; ++j)
                {
                    if (bytes[i + j] != find[j])
                    {
                        break;
                    }

                    if (j == find.Length - 1)
                    {
                        from = i;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw new Exception("Byte sequence not found.");
            }

            var byteList = new List<byte>(bytes);

            byteList.RemoveRange(from, find.Length);
            byteList.InsertRange(from, replace);

            return byteList.ToArray();
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
                if (cdr[recordIndex].IsPackageSignatureFile)
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