// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    [Collection("Signing Funtional Test Collection")]
    public class SignedPackageVerifierTests
    {
        private const string _packageTamperedError = "Package integrity check failed. The package has been tampered.";
        private const string _packageUnsignedError = "Package is not signed.";
        private const string _packageInvalidSignatureError = "Package signature is invalid.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;

        public SignedPackageVerifierTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = _testFixture.TrustProviders;
        }

        [Fact]
        public async Task Signer_VerifyOnSignedPackageAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_FileDeletedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    zip.Entries.First().Delete();
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3002);
                    totalErrorIssues.First().Message.Should().Be(_packageTamperedError);
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_FileAddedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var newEntryData = "malicious code";
            var newEntryName = "malicious file";
            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                using (var newEntryStream = zip.CreateEntry(newEntryName).Open())
                using (var newEntryDataStream = new MemoryStream(Encoding.UTF8.GetBytes(newEntryData)))
                {
                    newEntryStream.Seek(offset: 0, origin: SeekOrigin.End);
                    newEntryDataStream.CopyTo(newEntryStream);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3002);
                    totalErrorIssues.First().Message.Should().Be(_packageTamperedError);
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_FileAppendedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var extraData = "tampering data";

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                using (var entryStream = zip.Entries.First().Open())
                using (var extraStream = new MemoryStream(Encoding.UTF8.GetBytes(extraData)))
                {
                    entryStream.Seek(offset: 0, origin: SeekOrigin.End);
                    extraStream.CopyTo(entryStream);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3002);
                    totalErrorIssues.First().Message.Should().Be(_packageTamperedError);
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_FileTruncatedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                using (var entryStream = zip.Entries.First().Open())
                {
                    entryStream.SetLength(entryStream.Length - 1);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3002);
                    totalErrorIssues.First().Message.Should().Be(_packageTamperedError);
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_FileMetadataModifiedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    var entry = zip.Entries.First();

                    // ZipArchiveEntry.LastWriteTime supports a resolution of two seconds.
                    // https://msdn.microsoft.com/en-us/library/system.io.compression.ziparchiveentry.lastwritetime(v=vs.110).aspx
                    entry.LastWriteTime = entry.LastWriteTime.AddSeconds(2);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3002);
                    totalErrorIssues.First().Message.Should().Be(_packageTamperedError);
                }           
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_SignatureRemovedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // unsign the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    var entry = zip.GetEntry(SigningSpecifications.V1.SignaturePath);
                    entry.Delete();
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3001);
                    totalErrorIssues.First().Message.Should().Be(_packageUnsignedError);
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_SignatureMetadataModifiedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // unsign the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    var entry = zip.GetEntry(SigningSpecifications.V1.SignaturePath);

                    // ZipArchiveEntry.LastWriteTime supports a resolution of two seconds.
                    // https://msdn.microsoft.com/en-us/library/system.io.compression.ziparchiveentry.lastwritetime(v=vs.110).aspx
                    entry.LastWriteTime = entry.LastWriteTime.AddSeconds(2);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    // No failure expected as the signature file or its metadata is not part of the original hash
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [Fact]
        public async Task Signer_VerifyOnTamperedPackage_SignatureTruncatedAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // tamper with the signature
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                using (var entryStream = zip.GetEntry(SigningSpecifications.V1.SignaturePath).Open())
                {
                    entryStream.SetLength(entryStream.Length - 1);
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3001);
                    totalErrorIssues.First().Message.Should().Be(_packageInvalidSignatureError);
                }
            }
        }
    }
}
#endif