// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Crypto.Parameters;
using Test.Utility.Signing;
using Xunit;

using DotNetUtilities = Org.BouncyCastle.Security.DotNetUtilities;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class RepositorySignatureVerifierTests
    {
        private readonly SigningTestFixture _fixture;
        private readonly RepositorySignatureVerifier _verifier;

        public RepositorySignatureVerifierTests(SigningTestFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
            _verifier = new RepositorySignatureVerifier();
        }

        [Fact]
        public async Task VerifyAsync_WithAuthorSignedPackage_Throws()
        {
            using (var test = await Test.CreateAuthorSignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => _verifier.VerifyAsync(packageReader, CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3000, exception.Code);
                Assert.Equal("The package does not have a repository signature (primary or countersignature).", exception.Message);
            }
        }

        [Collection(SigningTestCollection.Name)]
        public class RepositoryPrimarySignatures
        {
            private readonly SigningTestFixture _fixture;
            private readonly RepositorySignatureVerifier _verifier;

            public RepositoryPrimarySignatures(SigningTestFixture fixture)
            {
                if (fixture == null)
                {
                    throw new ArgumentNullException(nameof(fixture));
                }

                _fixture = fixture;
                _verifier = new RepositorySignatureVerifier();
            }

            [Fact]
            public async Task VerifyAsync_WithValidSignature_ReturnsValid()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithValidSignatureButNoTimestamp_ReturnsUntrusted()
            {
                using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Untrusted, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithUntrustedSignature_ReturnsUntrusted()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.UntrustedTestCertificate.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Untrusted, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithRevokedPrimaryCertificate_ReturnsSuspect()
            {
                var testServer = await _fixture.GetSigningTestServerAsync();
                var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                var issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
                var bcCertificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var certificate = new X509Certificate2(bcCertificate.GetEncoded()))
                {
                    certificate.PrivateKey = DotNetUtilities.ToRSA(issueCertificateOptions.KeyPair.Private as RsaPrivateCrtKeyParameters);

                    using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                        certificate,
                        timestampService.Url))
                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(bcCertificate);

                        certificateAuthority.Revoke(
                            bcCertificate,
                            RevocationReason.KeyCompromise,
                            DateTimeOffset.UtcNow.AddHours(-1));

                        var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Suspect, status);
                    }
                }
            }

            [Fact]
            public async Task VerifyAsync_WithRevokedTimestampCertificate_ReturnsSuspect()
            {
                var testServer = await _fixture.GetSigningTestServerAsync();
                var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                var timestampService = TimestampService.Create(certificateAuthority);

                using (testServer.RegisterResponder(timestampService))
                using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);

                    certificateAuthority.Revoke(
                        timestampService.Certificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Suspect, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithTamperedRepositoryPrimarySignedPackage_ReturnsSuspect()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                {
                    using (var stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Position = 0;

                        stream.WriteByte(0x00);
                    }

                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Suspect, status);
                    }
                }
            }
        }

        [Collection(SigningTestCollection.Name)]
        public class RepositoryCountersignatures
        {
            private readonly SigningTestFixture _fixture;
            private readonly RepositorySignatureVerifier _verifier;

            public RepositoryCountersignatures(SigningTestFixture fixture)
            {
                if (fixture == null)
                {
                    throw new ArgumentNullException(nameof(fixture));
                }

                _fixture = fixture;
                _verifier = new RepositorySignatureVerifier();
            }

            [Fact]
            public async Task VerifyAsync_WithValidCountersignature_ReturnsValid()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithValidCountersignatureAndUntrustedPrimarySignature_ReturnsValid()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.UntrustedTestCertificate.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithValidCountersignatureButNoTimestamp_ReturnsUntrusted()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Untrusted, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithUntrustedCountersignature_ReturnsUntrusted()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.UntrustedTestCertificate.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Untrusted, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithRevokedCountersignatureCertificate_ReturnsSuspect()
            {
                var testServer = await _fixture.GetSigningTestServerAsync();
                var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                var issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
                var bcCertificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var certificate = new X509Certificate2(bcCertificate.GetEncoded()))
                {
                    certificate.PrivateKey = DotNetUtilities.ToRSA(issueCertificateOptions.KeyPair.Private as RsaPrivateCrtKeyParameters);

                    using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                        _fixture.TrustedTestCertificate.Source.Cert,
                        certificate,
                        timestampService.Url,
                        timestampService.Url))
                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(bcCertificate);

                        certificateAuthority.Revoke(
                            bcCertificate,
                            RevocationReason.KeyCompromise,
                            DateTimeOffset.UtcNow.AddHours(-1));

                        var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Suspect, status);
                    }
                }
            }

            [Fact]
            public async Task VerifyAsync_WithRevokedTimestampCertificate_ReturnsSuspect()
            {
                var testServer = await _fixture.GetSigningTestServerAsync();
                var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();
                var revokedTimestampService = TimestampService.Create(certificateAuthority);

                using (testServer.RegisterResponder(revokedTimestampService))
                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    revokedTimestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(revokedTimestampService.Certificate);

                    certificateAuthority.Revoke(
                        revokedTimestampService.Certificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Suspect, status);
                }
            }

            [Fact]
            public async Task VerifyAsync_WithTamperedRepositoryCountersignedPackage_ReturnsSuspect()
            {
                var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                {
                    using (var stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Position = 0;

                        stream.WriteByte(0x00);
                    }

                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        var status = await _verifier.VerifyAsync(packageReader, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Suspect, status);
                    }
                }
            }
        }

        private sealed class Test : IDisposable
        {
            private readonly TestDirectory _directory;
            private bool _isDisposed;

            internal FileInfo PackageFile { get; }

            private Test(TestDirectory directory, FileInfo package)
            {
                _directory = directory;
                PackageFile = package;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<Test> CreateAuthorSignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateRepositoryPrimarySignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    new Uri("https://nuget.test"),
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateAuthorSignedRepositoryCountersignedPackageAsync(
                X509Certificate2 authorCertificate,
                X509Certificate2 repositoryCertificate,
                Uri authorTimestampServiceUrl = null,
                Uri repoTimestampServiceUrl = null)
            {
                var directory = TestDirectory.Create();

                using (var test = await CreateAuthorSignedPackageAsync(authorCertificate, authorTimestampServiceUrl))
                {
                    var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                        repositoryCertificate,
                        test.PackageFile.FullName,
                        directory,
                        new Uri("https://nuget.test"),
                        repoTimestampServiceUrl);

                    return new Test(directory, new FileInfo(signedPackagePath));
                }
            }
        }
    }
}
#endif