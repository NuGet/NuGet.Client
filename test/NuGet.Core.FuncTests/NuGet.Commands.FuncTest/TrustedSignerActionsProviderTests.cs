// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Configuration.Test;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Commands.FuncTest
{
    public class TrustedSignerActionsProviderTests : IClassFixture<SigningCommandsTestFixture>
    {
        private const string _expectedCertificateFingerprint = "3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece";
        private const string _errorRepoTrustExpectedRepoSignature = "Unable to add trusted repository. The package is not repository signed or countersigned.";
        private const string _errorTrustedSignerExists = "A trusted signer '{0}' already exists.";
        private const string _errorTrustedRepoExists = "A trusted repository with the service index '{0}' already exists.";
        private const string _errorAuthorTrustExpectedAuthorSignature = "Unable to add trusted author. The package is not author signed.";

        private readonly SigningCommandsTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _cert;
        private readonly TrustedTestCert<TestCertificate> _repoCert;

        public TrustedSignerActionsProviderTests(SigningCommandsTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _cert = fixture.TrustedTestCertificate;
            _repoCert = fixture.TrustedRepositoryCertificate;
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_TargetRepository_NonRepositorySignedPackage_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(trustedCert, packageContext, directory);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act and Assert
                    var ex = await Record.ExceptionAsync(async () =>
                        await actionsProvider.AddTrustedSignerAsync(
                            name: "signer",
                            package: packageReader,
                            trustTarget: VerificationTarget.Repository,
                            allowUntrustedRoot: false,
                            owners: null,
                            token: CancellationToken.None));

                    ex.Should().NotBeNull();
                    ex.Should().BeOfType<InvalidOperationException>();
                    ex.Message.Should().Be(_errorRepoTrustExpectedRepoSignature);
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_NameAlreadyExists_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new AuthorItem("author1", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(trustedCert, packageContext, directory);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act and Assert
                    var ex = await Record.ExceptionAsync(async () =>
                        await actionsProvider.AddTrustedSignerAsync(
                            name: "author1",
                            package: packageReader,
                            trustTarget: VerificationTarget.Author,
                            allowUntrustedRoot: false,
                            owners: null,
                            token: CancellationToken.None));

                    ex.Should().NotBeNull();
                    ex.Should().BeOfType<InvalidOperationException>();
                    ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, _errorTrustedSignerExists, "author1"));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_ServiceIndexAlreadyExists_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedv3serviceindex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", repoServiceIndex, new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCert, packageContext, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act and Assert
                    var ex = await Record.ExceptionAsync(async () =>
                        await actionsProvider.AddTrustedSignerAsync(
                            name: "repo2",
                            package: packageReader,
                            trustTarget: VerificationTarget.Repository,
                            allowUntrustedRoot: false,
                            owners: null,
                            token: CancellationToken.None));

                    ex.Should().NotBeNull();
                    ex.Should().BeOfType<InvalidOperationException>();
                    ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, _errorTrustedRepoExists, repoServiceIndex));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_WithUnknownPrimarySignature_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var packageReader = new Mock<ISignedPackageReader>();

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var content = new SignatureContent(
                     SigningSpecifications.V1,
                     HashAlgorithmName.SHA256,
                     hashValue: "hash");

                var contentInfo = new ContentInfo(content.GetBytes());
                var signedCms = new SignedCms(contentInfo);
                var cmsSigner = new CmsSigner(trustedCert);
                signedCms.ComputeSignature(cmsSigner);

                var signature = PrimarySignature.Load(signedCms.Encode());

                packageReader
                    .Setup(r => r.GetPrimarySignatureAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(signature));

                // Act and Assert
                var ex = await Record.ExceptionAsync(async () =>
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "author1",
                        package: packageReader.Object,
                        trustTarget: VerificationTarget.Author,
                        allowUntrustedRoot: false,
                        owners: null,
                        token: CancellationToken.None));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be(_errorAuthorTrustExpectedAuthorSignature);
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositorySignedPackage_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(trustedCert, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCert, packageContext, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: false,
                        owners: null,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            !i.Owners.Any() &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositorySignedPackage_WithOwners_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(trustedCert, HashAlgorithmName.SHA256);
                var expectedOwners = new List<string>() { "one", "two", "three" };

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCert, packageContext, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: false,
                        owners: expectedOwners,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            i.Owners.SequenceEqual(expectedOwners) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositorySignedPackage_WithAllowUntrustedRoot_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var trustedCert = new X509Certificate2(_cert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(trustedCert, HashAlgorithmName.SHA256);
                var expectedOwners = new List<string>() { "one", "two", "three" };

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCert, packageContext, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: true,
                        owners: expectedOwners,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: true);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            i.Owners.SequenceEqual(expectedOwners) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositoryCountersignedPackage_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var authorCert = new X509Certificate2(_cert.Source.Cert))
            using (var repoCert = new X509Certificate2(_repoCert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(repoCert, HashAlgorithmName.SHA256);

                var authorSignedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, packageContext, directory);
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoCert, authorSignedPackagePath, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: false,
                        owners: null,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: false);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            !i.Owners.Any() &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositoryCountersignedPackage_WithOwners_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var authorCert = new X509Certificate2(_cert.Source.Cert))
            using (var repoCert = new X509Certificate2(_repoCert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(repoCert, HashAlgorithmName.SHA256);
                var expectedOwners = new List<string>() { "one", "two", "three" };

                var authorSignedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, packageContext, directory);
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoCert, authorSignedPackagePath, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: false,
                        owners: expectedOwners,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: false);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            i.Owners.SequenceEqual(expectedOwners) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_RepositoryCountersignedPackage_WithAllowUntrustedRoot_AddsRepositoryCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();
            var repoServiceIndex = "https://trustedV3ServiceIndex.test/api.json";

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var authorCert = new X509Certificate2(_cert.Source.Cert))
            using (var repoCert = new X509Certificate2(_repoCert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(repoCert, HashAlgorithmName.SHA256);
                var expectedOwners = new List<string>() { "one", "two", "three" };

                var authorSignedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, packageContext, directory);
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoCert, authorSignedPackagePath, directory, new Uri(repoServiceIndex));

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "repo1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Repository,
                        allowUntrustedRoot: true,
                        owners: expectedOwners,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: true);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                            string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                            string.Equals(i.ServiceIndex, repoServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                            i.Owners.SequenceEqual(expectedOwners) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_AuthorSignedPackage_AddsAuthorCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var authorCert = new X509Certificate2(_cert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(authorCert, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, packageContext, directory);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "author1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Author,
                        allowUntrustedRoot: false,
                        owners: null,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: false);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<AuthorItem>(i =>
                            string.Equals(i.Name, "author1", StringComparison.Ordinal) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

        [CIOnlyFact]
        public async Task AddTrustedSignersAsync_AuthorSignedPackage_WithAllowUntrustedRoot_AddsAuthorCorrectlyAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageContext = new SimpleTestPackageContext();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            using (var directory = TestDirectory.Create())
            using (var authorCert = new X509Certificate2(_cert.Source.Cert))
            {
                var certFingerprint = CertificateUtility.GetHashString(authorCert, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, packageContext, directory);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    await actionsProvider.AddTrustedSignerAsync(
                        name: "author1",
                        package: packageReader,
                        trustTarget: VerificationTarget.Author,
                        allowUntrustedRoot: true,
                        owners: null,
                        token: CancellationToken.None);

                    var expectedCert = new CertificateItem(certFingerprint, HashAlgorithmName.SHA256, allowUntrustedRoot: true);

                    // Assert
                    trustedSignersProvider.Verify(p =>
                        p.AddOrUpdateTrustedSigner(It.Is<AuthorItem>(i =>
                            string.Equals(i.Name, "author1", StringComparison.Ordinal) &&
                            i.Certificates.Count == 1 &&
                            SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
                }
            }
        }

    }
}
