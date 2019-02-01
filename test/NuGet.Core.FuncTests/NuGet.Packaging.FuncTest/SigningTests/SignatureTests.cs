// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignatureTests
    {
        private readonly SigningSpecifications _specification = SigningSpecifications.V1;
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TestCertificate _untrustedTestCertificate;

        public SignatureTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task Verify_WithUntrustedSelfSignedCertificateAndNotAllowUntrusted_FailsAsync()
        {
            var settings = new SignatureVerifySettings(
                allowIllegal: false,
                allowUntrusted: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                reportUntrustedRoot: true,
                revocationMode: RevocationMode.Online);

            using (var test = await VerifyTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var result = test.PrimarySignature.Verify(
                    timestamp: null,
                    settings: settings,
                    fingerprintAlgorithm: HashAlgorithmName.SHA256,
                    certificateExtraStore: test.PrimarySignature.SignedCms.Certificates);

                Assert.Equal(SignatureVerificationStatus.Disallowed, result.Status);
                Assert.Equal(RuntimeEnvironmentHelper.IsLinux ? 2 : 1, result.Issues.Count(issue => issue.Level == LogLevel.Error));

                SigningTestUtility.AssertUntrustedRoot(result.Issues, LogLevel.Error);

                if (RuntimeEnvironmentHelper.IsLinux)
                {
                    SigningTestUtility.AssertOfflineRevocation(result.Issues, LogLevel.Error);
                }
            }
        }

        [CIOnlyFact]
        public async Task Verify_WithUntrustedSelfSignedCertificateAndAllowUntrusted_SucceedsAndWarnsAsync()
        {
            var settings = new SignatureVerifySettings(
                allowIllegal: false,
                allowUntrusted: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                reportUntrustedRoot: true,
                revocationMode: RevocationMode.Online);

            using (var test = await VerifyTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var result = test.PrimarySignature.Verify(
                    timestamp: null,
                    settings: settings,
                    fingerprintAlgorithm: HashAlgorithmName.SHA256,
                    certificateExtraStore: test.PrimarySignature.SignedCms.Certificates);

                Assert.Equal(SignatureVerificationStatus.Valid, result.Status);
                Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                Assert.Equal(RuntimeEnvironmentHelper.IsLinux ? 2 : 1, result.Issues.Count(issue => issue.Level == LogLevel.Warning));
            }
        }

        [CIOnlyFact]
        public async Task Verify_WithUntrustedSelfSignedCertificateAndAllowUntrustedAndNotReportUntrustedRoot_SucceedsAsync()
        {
            var settings = new SignatureVerifySettings(
                allowIllegal: false,
                allowUntrusted: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: false,
                reportUntrustedRoot: false,
                revocationMode: RevocationMode.Online);

            using (var test = await VerifyTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var result = test.PrimarySignature.Verify(
                    timestamp: null,
                    settings: settings,
                    fingerprintAlgorithm: HashAlgorithmName.SHA256,
                    certificateExtraStore: test.PrimarySignature.SignedCms.Certificates);

                Assert.Equal(SignatureVerificationStatus.Valid, result.Status);
                Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Warning));
            }
        }

        [CIOnlyFact]
        public async Task GetSigningCertificateFingerprint_WithUnsupportedHashAlgorithm_Throws()
        {
            using (var test = await VerifyTest.CreateAsync(settings: null, certificate: _untrustedTestCertificate.Cert))
            {
                Assert.Throws(typeof(ArgumentException),
                    () => test.PrimarySignature.GetSigningCertificateFingerprint((HashAlgorithmName)99));
            }   
        }

        [CIOnlyFact]
        public async Task GetSigningCertificateFingerprint_SuccessfullyHashesMultipleAlgorithms()
        {
            using (var test = await VerifyTest.CreateAsync(settings: null, certificate: _untrustedTestCertificate.Cert))
            {
                var sha256 = test.PrimarySignature.GetSigningCertificateFingerprint(HashAlgorithmName.SHA256);
                var sha384 = test.PrimarySignature.GetSigningCertificateFingerprint(HashAlgorithmName.SHA384);
                var sha512 = test.PrimarySignature.GetSigningCertificateFingerprint(HashAlgorithmName.SHA512);

                var expectedSha256 = SignatureTestUtility.GetFingerprint(_untrustedTestCertificate.Cert, HashAlgorithmName.SHA256);
                var expectedSha384 = SignatureTestUtility.GetFingerprint(_untrustedTestCertificate.Cert, HashAlgorithmName.SHA384);
                var expectedSha512 = SignatureTestUtility.GetFingerprint(_untrustedTestCertificate.Cert, HashAlgorithmName.SHA512);

                Assert.Equal(sha256, expectedSha256, StringComparer.Ordinal);
                Assert.Equal(sha384, expectedSha384, StringComparer.Ordinal);
                Assert.Equal(sha512, expectedSha512, StringComparer.Ordinal);
            }
        }

        private sealed class VerifyTest : IDisposable
        {
            private readonly TestDirectory _directory;

            private bool _isDisposed;

            internal SignedPackageArchive Package { get; }
            internal SignatureVerifySettings Settings { get; }
            internal PrimarySignature PrimarySignature { get; }

            private VerifyTest(
                TestDirectory directory,
                SignedPackageArchive package,
                PrimarySignature primarySignature,
                SignatureVerifySettings settings)
            {
                _directory = directory;
                Package = package;
                PrimarySignature = primarySignature;
                Settings = settings;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();
                    Package.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<VerifyTest> CreateAsync(SignatureVerifySettings settings, X509Certificate2 certificate)
            {
                using (var certificateClone = new X509Certificate2(certificate))
                {
                    var directory = TestDirectory.Create();
                    var packageContext = new SimpleTestPackageContext();
                    var unsignedPackageFile = await packageContext.CreateAsFileAsync(directory, "package.nupkg");
                    var signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                        directory,
                        unsignedPackageFile,
                        certificateClone);
                    var package = new SignedPackageArchive(signedPackageFile.OpenRead(), new MemoryStream());
                    var primarySignature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                    return new VerifyTest(directory, package, primarySignature, settings);
                }
            }
        }
    }
}