// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

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
        private SigningSpecifications _specification => SigningSpecifications.V1;

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TestCertificate _untrustedTestCertificate;

        public SignatureTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
        }


        [CIOnlyFact]
        public async Task Verify_WithUntrustedSelfSignedCertificateAndNotAllowUntrustedSelfSignedCertificate_FailsAsync()
        {
            var settings = new SignatureVerifySettings(
                treatIssueAsError: true,
                allowUntrustedRoot: false,
                allowUnknownRevocation: false);

            using (var test = await VerifyTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var issues = new List<SignatureLog>();
                var result = test.PrimarySignature.Verify(null, settings, HashAlgorithmName.SHA256, test.PrimarySignature.SignedCms.Certificates, issues);

                Assert.Equal(SignatureVerificationStatus.Illegal, result.Status);
                Assert.Equal(1, issues.Count(issue => issue.Level == LogLevel.Error));

                AssertUntrustedRoot(issues, LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task Verify_WithUntrustedSelfSignedCertificateAndAllowUntrustedSelfSignedCertificate_SucceedsAsync()
        {
            var settings = new SignatureVerifySettings(
                treatIssueAsError: true,
                allowUntrustedRoot: true,
                allowUnknownRevocation: false);

            using (var test = await VerifyTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var issues = new List<SignatureLog>();
                var result = test.PrimarySignature.Verify(null, settings, HashAlgorithmName.SHA256, test.PrimarySignature.SignedCms.Certificates, issues);

                Assert.Equal(SignatureVerificationStatus.Valid, result.Status);
                Assert.Equal(0, issues.Count(issue => issue.Level == LogLevel.Error));
            }
        }

        private static void AssertUntrustedRoot(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.");
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
                    var unsignedPackageFile = packageContext.CreateAsFile(directory, "package.nupkg");
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

#endif