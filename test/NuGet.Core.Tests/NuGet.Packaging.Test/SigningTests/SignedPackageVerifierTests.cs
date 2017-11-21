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

namespace NuGet.Packaging.Test.SigningTests
{
    public class SignedPackageVerifierTests
    {
        private const string _packageTamperedError = "Package integrity check failed. The package has been tampered.";
        private const string _packageUnsignedError = "Package is not signed.";

        private static readonly IList<ISignatureVerificationProvider> _trustProviders = new List<ISignatureVerificationProvider>()
        {
            new X509SignatureVerificationProvider(),
            new NuGetIntegrityVerificationProvider(),
            new TimestampVerificationProvider()
        };

        private static readonly SigningSpecifications _signingSpecifications = SigningSpecifications.V1;  

        [Fact]
        public async Task Signer_VerifyOnSignedPackageAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {                
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);

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
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

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
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);
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
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);
                var extraData = "tampering data";

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
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);
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
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

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
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);
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
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

                // tamper with the package
                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    var entry = zip.Entries.First();
                    entry.LastWriteTime = DateTimeOffset.Now;
                }

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);
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
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

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
                    var result = await verifier.VerifySignaturesAsync(packageReader, testLogger, CancellationToken.None);
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
    }
}
#endif