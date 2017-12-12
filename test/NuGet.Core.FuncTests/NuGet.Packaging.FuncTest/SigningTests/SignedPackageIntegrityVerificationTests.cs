// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Funtional Test Collection")]
    public class SignedPackageIntegrityVerificationTests
    {
        private SigningSpecifications _specification => SigningSpecifications.V1;

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;

        public SignedPackageIntegrityVerificationTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider()
            };
        }

        [CIOnlyFact]
        public async Task VerifyOnShiftedSignaturePackage_WhenCentralDirectoryIsLastAndFileHeaderIsLastAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignedArchiveTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, entryCount - 1, entryCount - 1);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
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
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);

                await SignedArchiveTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, 0, 0);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
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
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignedArchiveTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, entryCount - 1, 0);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
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
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                await SignedArchiveTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, 0, entryCount - 1);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
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
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var entryCount = 0;

                using (var stream = File.Open(signedPackagePath, FileMode.Open))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    entryCount = zip.Entries.Count;
                }

                var middleEntry = (entryCount - 1) / 2;
                await SignedArchiveTestUtility.ShiftSignatureMetadataAsync(_specification, signedPackagePath, dir, middleEntry - 1, middleEntry + 1);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.RequireSigned);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
    }
}

#endif