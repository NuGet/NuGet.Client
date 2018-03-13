// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        /// <param name="name">Name of the signed package file to create.</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedPackageAsync(X509Certificate2 testCert, SimpleTestPackageContext nupkg, string dir, string name = null)
        {
            var testLogger = new TestLogger();
            var signedPackagePath = Path.Combine(dir, name ?? Guid.NewGuid().ToString());
            var tempPath = Path.GetTempFileName();

            using (var packageStream = nupkg.CreateAsStream())
            using (var fileStream = File.OpenWrite(tempPath))
            {
                packageStream.CopyTo(fileStream);
            }

            await SignPackageAsync(testLogger, testCert, tempPath, signedPackagePath);

            FileUtility.Delete(tempPath);

            return signedPackagePath;
        }

        public static async Task CreateSignedPackageAsync(
            SignPackageRequest request,
            Stream packageReadStream,
            Stream packageWriteStream)
        {
            using (var signedPackage = new SignedPackageArchive(packageReadStream, packageWriteStream))
            using (var options = new SigningOptions(
                new Lazy<Stream>(() => packageReadStream),
                new Lazy<Stream>(() => packageWriteStream),
                overwrite: false,
                signatureProvider: new X509SignatureProvider(timestampProvider: null),
                logger: NullLogger.Instance))
            {
                await SigningUtility.SignAsync(options, request, CancellationToken.None);
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
            AuthorSignPackageRequest request = null)
        {
            var testLogger = new TestLogger();
            var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());
            var tempPath = Path.GetTempFileName();

            using (var packageStream = nupkg.CreateAsStream())
            using (var fileStream = File.OpenWrite(tempPath))
            {
                packageStream.CopyTo(fileStream);
            }
#if IS_DESKTOP
            using (var cert = new X509Certificate2(certificate))
            using (request = request ?? new AuthorSignPackageRequest(cert, HashAlgorithmName.SHA256))
            {
                await SignAndTimeStampPackageAsync(testLogger, tempPath, signedPackagePath, timestampService, request);
            }
#endif

            FileUtility.Delete(tempPath);

            return signedPackagePath;
        }

        /// <summary>
        /// Generates a Signature for a package.
        /// </summary>
        /// <param name="testCert">Certificate to be used while generating the signature.</param>
        /// <param name="packageStream">Package stream for which the signature has to be generated.</param>
        /// <param name="timestampProvider">An optional timestamp provider.</param>
        /// <returns>Signature for the package.</returns>
        public static async Task<PrimarySignature> CreatePrimarySignatureForPackageAsync(
            X509Certificate2 testCert,
            Stream packageStream,
            ITimestampProvider timestampProvider = null)
        {
            var testLogger = new TestLogger();
            var hashAlgorithm = HashAlgorithmName.SHA256;

            using (var request = new AuthorSignPackageRequest(testCert, hashAlgorithm))
            using (var package = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var zipArchiveHash = await package.GetArchiveHashAsync(request.SignatureHashAlgorithm, CancellationToken.None);
                var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);
                var signatureContent = new SignatureContent(SigningSpecifications.V1, hashAlgorithm, base64ZipArchiveHash);
                var testSignatureProvider = new X509SignatureProvider(timestampProvider);

                return await testSignatureProvider.CreatePrimarySignatureAsync(request, signatureContent, testLogger, CancellationToken.None);
            }
        }

        public static async Task<bool> IsSignedAsync(Stream package)
        {
            var currentPosition = package.Position;

            using (var reader = new PackageArchiveReader(package, leaveStreamOpen: true))
            {
                var isSigned = await reader.IsSignedAsync(CancellationToken.None);

                package.Seek(offset: currentPosition, origin: SeekOrigin.Begin);

                return isSigned;
            }
        }

        public static async Task<bool> IsRepositoryCountersignedAsync(Stream package)
        {
            using (var reader = new PackageArchiveReader(package, leaveStreamOpen: true))
            {
                var primarySignature = await reader.GetPrimarySignatureAsync(CancellationToken.None);
                if (primarySignature != null)
                {
#if IS_DESKTOP
                    return RepositoryCountersignature.HasRepositoryCounterSignature(primarySignature);
#endif
                }

                return false;
            }
        }


        /// <summary>
        /// Adds a Repository countersignature to a given primary signature
        /// </summary>
        /// <param name="testCert">Certificate to be used while generating the countersignature.</param>
        /// <param name="primarySignature">Primary signature to add countersignature.</param>
        /// <param name="timestampProvider">An optional timestamp provider.</param>
        /// <returns></returns>
        public static async Task<PrimarySignature> RepositoryCountersignPrimarySignatureAsync(
            X509Certificate2 testCert,
            PrimarySignature primarySignature,
            ITimestampProvider timestampProvider = null)
        {
            var testLogger = new TestLogger();
            var hashAlgorithm = HashAlgorithmName.SHA256;
            var v3ServiceIndexUrl = new Uri("https://testv3serviceIndex.url/api/index.json");

            using (var request = new RepositorySignPackageRequest(testCert, hashAlgorithm, hashAlgorithm, v3ServiceIndexUrl, null))
            {
                var testSignatureProvider = new X509SignatureProvider(timestampProvider);

                return await testSignatureProvider.CreateRepositoryCountersignatureAsync(request, primarySignature, testLogger, CancellationToken.None);
            }
        }

        /// <summary>
        /// Sign a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        private static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 certificate, string inputPackagePath, string outputPackagePath)
        {
#if IS_DESKTOP
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);
            using (var cert = new X509Certificate2(certificate))
            using (var request = new AuthorSignPackageRequest(cert, HashAlgorithmName.SHA256))
            {
                const bool overwrite = false;
                using (var options = SigningOptions.CreateFromFilePaths(
                    inputPackagePath,
                    outputPackagePath,
                    overwrite,
                    testSignatureProvider,
                    testLogger))
                {
                    await SigningUtility.SignAsync(options, request, CancellationToken.None);
                }
            }
#endif
        }

        /// <summary>
        /// Sign and timestamp a package for test purposes.
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        private static async Task SignAndTimeStampPackageAsync(
            TestLogger testLogger,
            string inputPackagePath,
            string outputPackagePath,
            Uri timestampService,
            AuthorSignPackageRequest request)
        {
            var testSignatureProvider = new X509SignatureProvider(new Rfc3161TimestampProvider(timestampService));
            var overwrite = false;

            using (var options = SigningOptions.CreateFromFilePaths(
                inputPackagePath,
                outputPackagePath,
                overwrite,
                testSignatureProvider,
                testLogger))
            {
                await SigningUtility.SignAsync(options, request, CancellationToken.None);
            }
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
        public static async Task<PrimarySignature> CreatePrimarySignatureForPackageAsync(ISignatureProvider signatureProvider, PackageArchiveReader package, SignPackageRequest request, TestLogger testLogger)
        {
            var zipArchiveHash = await package.GetArchiveHashAsync(request.SignatureHashAlgorithm, CancellationToken.None);
            var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);
            var signatureContent = new SignatureContent(SigningSpecifications.V1, request.SignatureHashAlgorithm, base64ZipArchiveHash);

            return await signatureProvider.CreatePrimarySignatureAsync(request, signatureContent, testLogger, CancellationToken.None);
        }

        /// <summary>
        /// Adds a repository countersignature for a given primary signature for tests.
        /// </summary>
        /// <param name="signatureProvider">Signature proivider to create the repository countersignature.</param>
        /// <param name="signature">Primary signature to add the repository countersignature.</param>
        /// <param name="request">RepositorySignPackageRequest containing the metadata for the signature request.</param>
        /// <param name="testLogger">ILogger.</param>
        /// <returns>Primary signature with a repository countersignature.</returns>
        public static async Task<PrimarySignature> RepositoryCountersignPrimarySignatureAsync(ISignatureProvider signatureProvider, PrimarySignature signature, RepositorySignPackageRequest request, TestLogger testLogger)
        {
            return await signatureProvider.CreateRepositoryCountersignatureAsync(request, signature, testLogger, CancellationToken.None);
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

        /// <summary>
        /// Timestamps a signature for tests.
        /// </summary>
        /// <param name="timestampProvider">Timestamp provider.</param>
        /// <param name="signatureRequest">SignPackageRequest containing metadata for timestamp request.</param>
        /// <param name="signature">Signature that needs to be timestamped.</param>
        /// <param name="logger">ILogger.</param>
        /// <returns>Timestamped Signature.</returns>
        public static Task<PrimarySignature> TimestampPrimarySignature(ITimestampProvider timestampProvider, SignPackageRequest signatureRequest, PrimarySignature signature, ILogger logger)
        {
            var signatureValue = signature.GetSignatureValue();
            var messageHash = signatureRequest.TimestampHashAlgorithm.ComputeHash(signatureValue);

            var timestampRequest = new TimestampRequest(
                signingSpec: SigningSpecifications.V1,
                signatureMessageHash: messageHash,
                timestampHashAlgorithm: signatureRequest.TimestampHashAlgorithm,
                timestampSignaturePlacement: SignaturePlacement.PrimarySignature
            );

            return TimestampPrimarySignature(timestampProvider, timestampRequest, signature, logger);
        }
#endif
        /// <summary>
        /// Timestamps a signature for tests.
        /// </summary>
        /// <param name="timestampProvider">Timestamp provider.</param>
        /// <param name="signature">Signature that needs to be timestamped.</param>
        /// <param name="logger">ILogger.</param>
        /// <param name="timestampRequest">timestampRequest containing metadata for timestamp request.</param>
        /// <returns>Timestamped Signature.</returns>
        public static Task<PrimarySignature> TimestampPrimarySignature(ITimestampProvider timestampProvider, TimestampRequest timestampRequest, PrimarySignature signature, ILogger logger)
        {
            return timestampProvider.TimestampSignatureAsync(signature, timestampRequest, logger, CancellationToken.None);
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

        public static void TamperWithPackage(string signedPackagePath)
        {
            using (var stream = File.Open(signedPackagePath, FileMode.Open))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
            {
                zip.Entries.First().Delete();
            }
        }

#if IS_DESKTOP

        public static PrimarySignature GenerateInvalidPrimarySignature(PrimarySignature signature)
        {
            var hash = Encoding.UTF8.GetBytes(signature.SignatureContent.HashValue);
            var newHash = Encoding.UTF8.GetBytes(new string('0', hash.Length));

            var bytes = signature.SignedCms.Encode();
            var newBytes = FindAndReplaceSequence(bytes, hash, newHash);

            return PrimarySignature.Load(newBytes);
        }
#endif

        public static byte[] FindAndReplaceSequence(byte[] bytes, byte[] find, byte[] replace)
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
            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

            // Calculate new central directory record metadata with the the signature record and entry shifted
            var shiftedCdr = ShiftMetadata(spec, metadata, newSignatureFileEntryIndex: fileHeaderIndex, newSignatureCentralDirectoryRecordIndex: centralDirectoryIndex);

            // Order records by shifted ofset (new offset = old offset + change in offset).
            // This is the order they will appear in the new shifted package, but not necesarily the same order they were in the old package
            shiftedCdr.Sort((x, y) => (x.OffsetToLocalFileHeader + x.ChangeInOffset).CompareTo(y.OffsetToLocalFileHeader + y.ChangeInOffset));

            // Write data from start of file to first file entry
            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, metadata.StartOfLocalFileHeaders);

            // Write all file entries in the new order
            foreach (var entry in shiftedCdr)
            {
                // We need to read each entry from their position in the old package and write them sequencially to the new package
                // The order in which they will appear in the new shited package is defined by the sorting done before starting to write
                reader.BaseStream.Seek(offset: entry.OffsetToLocalFileHeader, origin: SeekOrigin.Begin);
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, entry.OffsetToLocalFileHeader + entry.FileEntryTotalSize);
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
            shiftedCdr.Sort((x, y) => x.OffsetToLocalFileHeader.CompareTo(y.OffsetToLocalFileHeader));

            // Shift Central Directory records to the desired position.
            // Because we sorted in the file entry order this will shift
            // the file entries
            ShiftSignatureToIndex(spec, shiftedCdr, newSignatureFileEntryIndex);

            // Calculate the change in offsets for the shifted file entries
            var lastEntryEnd = 0L;
            foreach (var cdr in shiftedCdr)
            {
                cdr.ChangeInOffset = lastEntryEnd - cdr.OffsetToLocalFileHeader;

                lastEntryEnd = cdr.OffsetToLocalFileHeader + cdr.FileEntryTotalSize + cdr.ChangeInOffset;
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