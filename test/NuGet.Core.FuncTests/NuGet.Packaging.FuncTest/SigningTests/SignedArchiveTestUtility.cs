// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    internal static class SignedArchiveTestUtility
    {
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

            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipWriteStream))
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
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedAndTimeStampedPackageAsync(TrustedTestCert<TestCertificate> testCert, SimpleTestPackageContext nupkg, string dir)
        {
            var testLogger = new TestLogger();

            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipWriteStream))
                {
                    // Sign the package
                    await SignAndTimeStampPackageAsync(testLogger, testCert.Source.Cert, signPackage);
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

            using (var zipWriteStream = File.Open(copiedSignedPackagePath, FileMode.Open))
            using (var signedPackage = new SignedPackageArchive(zipWriteStream))
            {
                var signer = new Signer(signedPackage, testSignatureProvider);
                await signer.RemoveSignaturesAsync(testLogger, CancellationToken.None);
            }

            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
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

            var copiedSignedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

            using (var signedReadStream = File.OpenRead(signedPackagePath))
            using (var signedPackage = new BinaryReader(signedReadStream))
            using (var shiftedWriteStream = File.OpenWrite(copiedSignedPackagePath))
            using (var shiftedPackage = new BinaryWriter(shiftedWriteStream))
            {
                await ShiftSignatureMetadata(spec, signedPackage, shiftedPackage, centralDirectoryIndex, fileHeaderIndex);
            }

            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
        }

        private static Task ShiftSignatureMetadata(SigningSpecifications spec, BinaryReader reader, BinaryWriter writer, int centralDirectoryIndex, int fileHeaderIndex)
        {
            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
            var shiftedCdr = ShiftMetadata(spec, metadata, newSignatureFEIndex: fileHeaderIndex, newSignatureCDRIndex: centralDirectoryIndex);

            shiftedCdr.Sort((x, y) =>
                Comparer<long>.Default.Compare(
                    x.OffsetToFileHeader + x.ChangeInOffset,
                    y.OffsetToFileHeader + y.ChangeInOffset));

            // Write data from start of file to first file entry
            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            ReadAndWriteUntilPosition(reader, writer, metadata.StartOfFileHeaders);

            // Write all file entries in the new order
            foreach (var entry in shiftedCdr)
            {
                reader.BaseStream.Seek(offset: entry.OffsetToFileHeader, origin: SeekOrigin.Begin);
                ReadAndWriteUntilPosition(reader, writer, entry.OffsetToFileHeader + entry.FileEntryTotalSize);
            }

            // Write all central directory records with updated offsets
            shiftedCdr.Sort((x, y) => Comparer<long>.Default.Compare(x.IndexInRecords, y.IndexInRecords));

            reader.BaseStream.Seek(offset: metadata.StartOfCentralDirectory, origin: SeekOrigin.Begin);
            foreach (var entry in shiftedCdr)
            {
                reader.BaseStream.Seek(offset: entry.Position, origin: SeekOrigin.Begin);
                ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + 28);

                var filenameLength = reader.ReadUInt16();
                writer.Write(filenameLength);

                var extraFieldLength = reader.ReadUInt16();
                writer.Write(extraFieldLength);

                var fileCommentLength = reader.ReadUInt16();
                writer.Write(fileCommentLength);

                ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + 8);

                var relativeOffsetOfLocalFileHeader = (uint)(reader.ReadUInt32() + entry.ChangeInOffset);
                writer.Write(relativeOffsetOfLocalFileHeader);

                ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + filenameLength + extraFieldLength + fileCommentLength);
            }

            // Write everything after central directory records
            reader.BaseStream.Seek(offset: metadata.EndOfCentralDirectory, origin: SeekOrigin.Begin);
            ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Length);

            return Task.FromResult(0);
        }

        private static List<CentralDirectoryMetadata> ShiftMetadata(
            SigningSpecifications spec,
            SignedPackageArchiveMetadata metadata,
            int newSignatureFEIndex,
            int newSignatureCDRIndex)
        {
            var shiftedCdr = new List<CentralDirectoryMetadata>(metadata.CentralDirectoryRecords);

            shiftedCdr.Sort((x, y) => Comparer<long>.Default.Compare(x.OffsetToFileHeader, y.OffsetToFileHeader));
            ShiftSignatureCDRToIndex(spec, shiftedCdr, newSignatureFEIndex);

            var lastEntryEnd = 0L;
            foreach (var cdr in shiftedCdr)
            {
                cdr.ChangeInOffset = lastEntryEnd - cdr.OffsetToFileHeader;

                lastEntryEnd = cdr.OffsetToFileHeader + cdr.FileEntryTotalSize + cdr.ChangeInOffset;
            }

            shiftedCdr.Sort((x, y) => Comparer<long>.Default.Compare(x.Position, y.Position));
            ShiftSignatureCDRToIndex(spec, shiftedCdr, newSignatureCDRIndex);

            var lastIndex = 0;
            foreach (var cdr in shiftedCdr)
            {
                cdr.IndexInRecords = lastIndex;
                lastIndex++;
            }

            return shiftedCdr;
        }

        private static void ShiftSignatureCDRToIndex(
            SigningSpecifications spec,
            List<CentralDirectoryMetadata> cdr,
            int index)
        {
            var signatureCD = cdr.SingleOrDefault(cd => string.Equals(cd.Filename, spec.SignaturePath));
            if (signatureCD != null)
            {
                cdr.Remove(signatureCD);

                cdr.Insert(index, signatureCD);
            }
        }

        private static void ReadAndWriteUntilPosition(BinaryReader reader, BinaryWriter writer, long position)
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
    }
}
