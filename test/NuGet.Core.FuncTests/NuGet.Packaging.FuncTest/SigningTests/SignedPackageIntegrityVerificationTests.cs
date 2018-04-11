// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignedPackageIntegrityVerificationTests
    {
        private readonly UTF8Encoding _readerEncoding = new UTF8Encoding();
        private readonly UTF8Encoding _writerEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private SigningSpecifications _specification => SigningSpecifications.V1;

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;

        private readonly SignedPackageVerifierSettings _settings;

        public SignedPackageIntegrityVerificationTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider()
            };

            _settings = new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true,
                alwaysVerifyCountersignature: false);
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsLastAndFileHeaderIsLastAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignatureTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, entryCount - 1, entryCount - 1);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsFirstAndFileHeaderIsFirstAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                await SignatureTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, 0, 0);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsLastAndFileHeaderIsFirstAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignatureTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, entryCount - 1, 0);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsFirstAndFileHeaderIsLastAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignatureTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, 0, entryCount - 1);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsMiddleAndFileHeaderIsMiddleAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                var middleEntry = (entryCount - 1) / 2;
                await SignatureTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, middleEntry - 1, middleEntry + 1);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureIsCreatedUsingZipArchive()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var packageStream = nupkg.CreateAsStream())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signarture = await SignedArchiveTestUtility.CreateAuthorSignatureForPackageAsync(testCertificate, packageStream);
                using (var package = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var signatureEntry = package.CreateEntry(_specification.SignaturePath);
                    using (var signatureStream = new MemoryStream(signarture.GetBytes()))
                    using (var signatureEntryStream = signatureEntry.Open())
                    {
                        signatureStream.CopyTo(signatureEntryStream);
                    }
                }

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureIsCreatedUsingZipArchiveAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var packageStream = nupkg.CreateAsStream())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signarture = await SignedArchiveTestUtility.CreateAuthorSignatureForPackageAsync(testCertificate, packageStream);
                using (var package = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var signatureEntry = package.CreateEntry(_specification.SignaturePath);
                    using (var signatureStream = new MemoryStream(signarture.GetBytes()))
                    using (var signatureEntryStream = signatureEntry.Open())
                    {
                        signatureStream.CopyTo(signatureEntryStream);
                    }
                }

                // Assert
                AssertSignatureEntryMetadataThrowsException(packageStream);
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureIsCreatedUsingZipArchiveAndCompressed()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var packageStream = nupkg.CreateAsStream())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signarture = await SignedArchiveTestUtility.CreateAuthorSignatureForPackageAsync(testCertificate, packageStream);
                using (var package = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var signatureEntry = package.CreateEntry(_specification.SignaturePath, CompressionLevel.Optimal);
                    using (var signatureStream = new MemoryStream(signarture.GetBytes()))
                    using (var signatureEntryStream = signatureEntry.Open())
                    {
                        signatureStream.CopyTo(signatureEntryStream);
                    }
                }

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureIsCreatedUsingZipArchiveAndCompressedAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var packageStream = nupkg.CreateAsStream())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signarture = await SignedArchiveTestUtility.CreateAuthorSignatureForPackageAsync(testCertificate, packageStream);
                using (var package = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var signatureEntry = package.CreateEntry(_specification.SignaturePath, CompressionLevel.Optimal);
                    using (var signatureStream = new MemoryStream(signarture.GetBytes()))
                    using (var signatureEntryStream = signatureEntry.Open())
                    {
                        signatureStream.CopyTo(signatureEntryStream);
                    }
                }

                // Assert
                AssertSignatureEntryMetadataThrowsException(packageStream);
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidExternalFileAttributes()
        {
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 38L, origin: SeekOrigin.Begin);

                        // change external file attributes
                        writer.Write(0x20U);
                    }

                    AssertSignatureEntryMetadataThrowsException(packageStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidGeneralPurposeFlagBits()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes) and 2 version fields (2 bytes each)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 8L, origin: SeekOrigin.Begin);

                        // change general purpose bit flag
                        writer.Write((ushort)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidGeneralPurposeFlagBitsAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes) and 2 version fields (2 bytes each)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 8L, origin: SeekOrigin.Begin);

                        // change general purpose bit flag
                        writer.Write((ushort)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidCompressionMethod()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes), 2 version fields (2 bytes each) and general purpose bit field (2 bytes)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 10L, origin: SeekOrigin.Begin);

                        // change compression method
                        writer.Write((ushort)8);

                        // Assert
                        AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidCompressedSize()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till uncompressed size (20 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 24L, origin: SeekOrigin.Begin);

                        // change uncompressed size
                        writer.Write((uint)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidCompressedSizeAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till compressed size (20 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 20L, origin: SeekOrigin.Begin);

                        // change compressed size
                        writer.Write((uint)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidUncompressedSize()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till uncompressed size (24 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 24L, origin: SeekOrigin.Begin);

                        // change uncompressed size
                        writer.Write((uint)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureCentralDirectoryHeaderHasInvalidUncompressedSizeAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till uncompressed size (24 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.Position + 24L, origin: SeekOrigin.Begin);

                        // change uncompressed size
                        writer.Write((uint)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidGeneralPurposeFlagBits()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes) and version field (2 bytes)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 6L, origin: SeekOrigin.Begin);

                        // change general purpose bit flag
                        writer.Write((ushort)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidGeneralPurposeFlagBitsAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes) and version field (2 bytes)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 6L, origin: SeekOrigin.Begin);

                        // change general purpose bit flag
                        writer.Write((ushort)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidCompressionMethod()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes), version fields(2 bytes) and general purpose bit field (2 bytes)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 8L, origin: SeekOrigin.Begin);

                        // change compression method
                        writer.Write((ushort)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidCompressionMethodAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip header signature (4 bytes), version fields(2 bytes) and general purpose bit field (2 bytes)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 8L, origin: SeekOrigin.Begin);

                        // change compression method
                        writer.Write((ushort)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidCompressedSize()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till compressed size (18 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 18L, origin: SeekOrigin.Begin);

                        // change compressed size
                        writer.Write((uint)1);
                    }

                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageWriteStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidCompressedSizeAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till compressed size (18 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 18L, origin: SeekOrigin.Begin);

                        // change compressed size
                        writer.Write((uint)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidUncompressedSize()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till uncompressed size (22 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 22L, origin: SeekOrigin.Begin);

                        // change uncompressed size
                        writer.Write((uint)1);
                    }
                }

                using (var packageStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    var verifier = new PackageSignatureVerifier(_trustProviders);
                    using (var packageReader = new PackageArchiveReader(packageStream))
                    {
                        // Act
                        var result = await verifier.VerifySignaturesAsync(packageReader, _settings, CancellationToken.None);
                        var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                        var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                        // Assert
                        result.Valid.Should().BeFalse();
                        resultsWithErrors.Count().Should().Be(1);
                        totalErrorIssues.Count().Should().Be(1);
                        totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3005);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifyOnInvalidSignatureFileEntry_SignatureLocalFileHeaderHasInvalidUncompressedSizeAssertException()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                using (var packageWriteStream = new FileStream(signedPackagePath, FileMode.Open))
                {
                    using (var reader = new BinaryReader(packageWriteStream, _readerEncoding, leaveOpen: true))
                    using (var writer = new BinaryWriter(packageWriteStream, _writerEncoding, leaveOpen: true))
                    {
                        var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);
                        var signatureCentralDirectoryHeader = metadata.CentralDirectoryHeaders[metadata.SignatureCentralDirectoryHeaderIndex];

                        // skip till uncompressed size (22 bytes total)
                        writer.BaseStream.Seek(offset: signatureCentralDirectoryHeader.OffsetToLocalFileHeader + 22L, origin: SeekOrigin.Begin);

                        // change uncompressed size
                        writer.Write((uint)1);
                    }

                    // Assert
                    AssertSignatureEntryMetadataThrowsException(packageWriteStream);
                }
            }
        }

        private void AssertSignatureEntryMetadataThrowsException(Stream packageStream)
        {
            using (var reader = new BinaryReader(packageStream, _readerEncoding, leaveOpen: true))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader));

                exception.AsLogMessage().Code.Should().Be(NuGetLogCode.NU3005);
            }
        }
    }
}

#endif