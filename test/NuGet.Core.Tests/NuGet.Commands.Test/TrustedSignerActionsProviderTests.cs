// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Configuration.Test;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class TrustedSignerActionsProviderTests
    {
        private const string _expectedCertificateFingerprint = "3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece";

        [Fact]
        public void TrustedSignerActionsProvider_Constructor_WithNullTrustedSignersProvider_Throws()
        {
            // Act and Assert
            Assert.Throws<ArgumentNullException>(() => new TrustedSignerActionsProvider(trustedSignersProvider: null, logger: NullLogger.Instance));
        }

        [Fact]
        public void TrustedSignerActionsProvider_Constructor_WithNullLogger_Throws()
        {
            // Act and Assert
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            Assert.Throws<ArgumentNullException>(() => new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SyncTrustedRepositoryAsync_WithNullOrEmptyName_ThrowsAsync(string name)
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await actionsProvider.SyncTrustedRepositoryAsync(name, CancellationToken.None));

            ex.Message.Should().Contain(Strings.ArgumentCannotBeNullOrEmpty);
            ex.ParamName.Should().Be("name");
        }

        [Fact]
        public async Task SyncTrustedRepositoryAsync_WithNonExistantRepositoryItem_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);


            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.SyncTrustedRepositoryAsync("repo2", CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedRepositoryDoesNotExist, "repo2"));
        }

        [Fact]
        public async Task SyncTrustedRepositoryAsync_RepositoryWithoutRepositorySignatureEndpoint_ThrowsAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.IndexWithFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", source, new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.SyncTrustedRepositoryAsync("repo1", CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidCertificateInformationFromServer, source));
        }

        [Fact]
        public async Task SyncTrustedRepositoryAsync_RepositoryWithoutCerts_ThrowsAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.RepoSignIndexJsonData },
                { "https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignDataEmptyCertInfo }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", source, new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.SyncTrustedRepositoryAsync("repo1", CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_EmptyCertificateListInRepository, source));
        }


        [Fact]
        public async Task SyncTrustedRepositoryAsync_WithExistingRepositoryItem_UpdatesCertificatesInItemWithLatestFromRepositoryAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.RepoSignIndexJsonData },
                { "https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", source, new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            var expectedCert = new CertificateItem(_expectedCertificateFingerprint, HashAlgorithmName.SHA256);

            // Act
            await actionsProvider.SyncTrustedRepositoryAsync("repo1", CancellationToken.None);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                    i.Certificates.Count == 1 &&
                    SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
        }

        [Fact]
        public async Task AddTrustedSignerAsync_WithNullPackage_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () =>
                await actionsProvider.AddTrustedSignerAsync(name: "signer", package: null, trustTarget: VerificationTarget.All, allowUntrustedRoot: false, owners: null, token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentNullException>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddTrustedSignerAsync_WithNullOrEmptyName_ThrowsAsync(string name)
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageReader = new Mock<ISignedPackageReader>();

            // Act and Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await actionsProvider.AddTrustedSignerAsync(
                    name,
                    packageReader.Object,
                    trustTarget: VerificationTarget.All,
                    allowUntrustedRoot: false,
                    owners: null,
                    token: CancellationToken.None));

            ex.Message.Should().Contain(Strings.ArgumentCannotBeNullOrEmpty);
            ex.ParamName.Should().Be("name");
        }

        [Fact]
        public async Task AddTrustedSignerAsync_WithUnknownTrustTarget_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageReader = new Mock<ISignedPackageReader>();

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () =>
                await actionsProvider.AddTrustedSignerAsync(
                    name: "signer",
                    package: packageReader.Object,
                    trustTarget: VerificationTarget.Unknown,
                    allowUntrustedRoot: false,
                    owners: null,
                    token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Contain(string.Format(CultureInfo.CurrentCulture, Strings.Error_UnsupportedTrustTarget, VerificationTarget.Unknown.ToString()));
        }

        [Fact]
        public async Task AddTrustedSignerAsync_WithUnsupportedTrustTarget_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageReader = new Mock<ISignedPackageReader>();

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () =>
                await actionsProvider.AddTrustedSignerAsync(
                    name: "signer",
                    package: packageReader.Object,
                    trustTarget: (VerificationTarget)99,
                    allowUntrustedRoot: false,
                    owners: null,
                    token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Contain(string.Format(CultureInfo.CurrentCulture, Strings.Error_UnsupportedTrustTarget, ((VerificationTarget)99).ToString()));
        }

        [Fact]
        public async Task AddTrustedSignerAsync_AuthorTrustTarget_SpecifyingOwners_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageReader = new Mock<ISignedPackageReader>();

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () =>
                await actionsProvider.AddTrustedSignerAsync(
                    name: "signer",
                    package: packageReader.Object,
                    trustTarget: VerificationTarget.Author,
                    allowUntrustedRoot: false,
                    owners: new List<string>() { "one", "two" },
                    token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Be(Strings.Error_TrustedAuthorNoOwners);
        }

        [Fact]
        public async Task AddTrustedSignersAsync_UnsignedPackage_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);
            var packageReader = new Mock<ISignedPackageReader>();

            packageReader
                .Setup(r => r.GetPrimarySignatureAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<PrimarySignature>(null));

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () =>
                await actionsProvider.AddTrustedSignerAsync(
                    name: "signer",
                    package: packageReader.Object,
                    trustTarget: VerificationTarget.Author,
                    allowUntrustedRoot: false,
                    owners: null,
                    token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(Strings.Error_PackageNotSigned);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AddOrUpdateTrustedSigner_WithNullOrEmptyName_Throws(string name)
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = Assert.Throws<ArgumentException>(() => actionsProvider.AddOrUpdateTrustedSigner(name, fingerprint: "abc", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: false));

            ex.Message.Should().Contain(Strings.ArgumentCannotBeNullOrEmpty);
            ex.ParamName.Should().Be("name");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AddOrUpdateTrustedSigner_WithNullOrEmptyFingerprint_Throws(string fingerprint)
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = Assert.Throws<ArgumentException>(() => actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: fingerprint, hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: false));

            ex.Message.Should().Contain(Strings.ArgumentCannotBeNullOrEmpty);
            ex.ParamName.Should().Be("fingerprint");
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_WithUnknownHashAlgorithm_Throws()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = Record.Exception(() => actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: "abc", hashAlgorithm: HashAlgorithmName.Unknown, allowUntrustedRoot: false));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Contain(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedHashAlgorithm, HashAlgorithmName.Unknown.ToString()));
        }


        [Fact]
        public void AddOrUpdateTrustedSigner_WithUnsupportedHashAlgorithm_Throws()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = Record.Exception(() => actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: "abc", hashAlgorithm: (HashAlgorithmName)99, allowUntrustedRoot: false));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Contain(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedHashAlgorithm, ((HashAlgorithmName)99).ToString()));
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_NewAuthor_AddsAuthorSuccesffully()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            var expectedCerts = new List<CertificateItem>()
            {
                new CertificateItem("def", HashAlgorithmName.SHA256),
            };

            // Act
            actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: "def", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: false);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<AuthorItem>(i =>
                    string.Equals(i.Name, "author1", StringComparison.Ordinal) &&
                    i.Certificates.Count == 1 &&
                    SettingsTestUtils.SequenceDeepEquals(i.Certificates.ToList(), expectedCerts))));
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_NewItem_AllowUntrustedRoot_AddsAuthorSuccesffully()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>());

            var expectedCerts = new List<CertificateItem>()
            {
                new CertificateItem("def", HashAlgorithmName.SHA256, allowUntrustedRoot: true),
            };

            // Act
            actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: "def", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: true);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<AuthorItem>(i =>
                    string.Equals(i.Name, "author1", StringComparison.Ordinal) &&
                    i.Certificates.Count == 1 &&
                    SettingsTestUtils.SequenceDeepEquals(i.Certificates.ToList(), expectedCerts))));
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_ExistingAuthor_UpdatesItemSuccessfully()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new AuthorItem("author1", new CertificateItem("abc", HashAlgorithmName.SHA256, allowUntrustedRoot: true))
                });


            var expectedCerts = new List<CertificateItem>()
            {
                new CertificateItem("abc", HashAlgorithmName.SHA256, allowUntrustedRoot: true),
                new CertificateItem("def", HashAlgorithmName.SHA256)
            };

            // Act
            actionsProvider.AddOrUpdateTrustedSigner(name: "author1", fingerprint: "def", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: false);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<AuthorItem>(i =>
                    string.Equals(i.Name, "author1", StringComparison.Ordinal) &&
                    i.Certificates.Count == 2 &&
                    SettingsTestUtils.SequenceDeepEquals(i.Certificates.ToList(), expectedCerts))));
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_ExistingItem_AllowUntrustedRoot_UpdatesItemSuccessfully()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });


            var expectedCerts = new List<CertificateItem>()
            {
                new CertificateItem("abc", HashAlgorithmName.SHA256),
                new CertificateItem("def", HashAlgorithmName.SHA256, allowUntrustedRoot: true)
            };

            // Act
            actionsProvider.AddOrUpdateTrustedSigner(name: "repo1", fingerprint: "def", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: true);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                    string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                    i.Certificates.Count == 2 &&
                    SettingsTestUtils.SequenceDeepEquals(i.Certificates.ToList(), expectedCerts))));
        }

        [Fact]
        public void AddOrUpdateTrustedSigner_ExistingRepository_UpdatesItemSuccessfully()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });


            var expectedCerts = new List<CertificateItem>()
            {
                new CertificateItem("abc", HashAlgorithmName.SHA256),
                new CertificateItem("def", HashAlgorithmName.SHA256)
            };

            // Act
            actionsProvider.AddOrUpdateTrustedSigner(name: "repo1", fingerprint: "def", hashAlgorithm: HashAlgorithmName.SHA256, allowUntrustedRoot: false);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                    string.Equals(i.Name, "repo1", StringComparison.Ordinal) &&
                    i.Certificates.Count == 2 &&
                    SettingsTestUtils.SequenceDeepEquals(i.Certificates.ToList(), expectedCerts))));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddTrustedRepositoryAsync_WithNullOrEmptyName_ThrowsAsync(string name)
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await actionsProvider.AddTrustedRepositoryAsync(name, new Uri("https://serviceIndex.test/v3/api.json"), owners: null, token: CancellationToken.None));

            ex.Message.Should().Contain(Strings.ArgumentCannotBeNullOrEmpty);
            ex.ParamName.Should().Be("name");
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_WithNullServiceIndex_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();
            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);

            // Act and Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await actionsProvider.AddTrustedRepositoryAsync("repo1", serviceIndex: null, owners: null, token: CancellationToken.None));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_WithNameAlreadyUsedInItem_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);


            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.AddTrustedRepositoryAsync("repo1", new Uri("https://serviceIndex.test/v3/api.json"), owners: null, token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedSignerAlreadyExists, "repo1"));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_WithServiceIndexAlreadyUsedInItem_ThrowsAsync()
        {
            // Arrange
            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance);


            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.AddTrustedRepositoryAsync("repo2", new Uri("https://serviceIndex.test/api.json"), owners: null, token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedRepoAlreadyExists, "https://serviceindex.test/api.json"));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_RepositoryWithoutRepositorySignatureEndpoint_ThrowsAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.IndexWithFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.AddTrustedRepositoryAsync("repo2", new Uri(source), owners: null, token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidCertificateInformationFromServer, source));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_RepositoryWithoutCerts_ThrowsAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.RepoSignIndexJsonData },
                { "https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignDataEmptyCertInfo }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            // Act and Assert
            var ex = await Record.ExceptionAsync(async () => await actionsProvider.AddTrustedRepositoryAsync("repo2", new Uri(source), owners: null, token: CancellationToken.None));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Error_EmptyCertificateListInRepository, source));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_AddsRepositoryItemSuccessfullyAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.RepoSignIndexJsonData },
                { "https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            var expectedCert = new CertificateItem(_expectedCertificateFingerprint, HashAlgorithmName.SHA256);

            // Act
            await actionsProvider.AddTrustedRepositoryAsync("repo2", new Uri(source), owners: null, token: CancellationToken.None);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                    string.Equals(i.Name, "repo2", StringComparison.Ordinal) &&
                    string.Equals(i.ServiceIndex, source, StringComparison.OrdinalIgnoreCase) &&
                    !i.Owners.Any() &&
                    i.Certificates.Count == 1 &&
                    SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
        }

        [Fact]
        public async Task AddTrustedRepositoryAsync_WithOwners_AddsRepositoryItemSuccessfullyAsync()
        {
            // Arrange
            var source = $"https://{Guid.NewGuid()}.unit.test/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>
            {
                { source, JsonData.RepoSignIndexJsonData },
                { "https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData }
            };

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            var trustedSignersProvider = new Mock<ITrustedSignersProvider>();

            trustedSignersProvider
                .Setup(p => p.GetTrustedSigners())
                .Returns(new List<TrustedSignerItem>()
                {
                    new RepositoryItem("repo1", "https://serviceIndex.test/api.json", new CertificateItem("abc", HashAlgorithmName.SHA256))
                });

            var actionsProvider = new TrustedSignerActionsProvider(trustedSignersProvider.Object, logger: NullLogger.Instance)
            {
                ServiceIndexSourceRepository = repo
            };

            var expectedCert = new CertificateItem(_expectedCertificateFingerprint, HashAlgorithmName.SHA256);
            var expectedOwners = new List<string>() { "one", "two", "three" };

            // Act
            await actionsProvider.AddTrustedRepositoryAsync("repo2", new Uri(source), owners: expectedOwners, token: CancellationToken.None);

            // Assert
            trustedSignersProvider.Verify(p =>
                p.AddOrUpdateTrustedSigner(It.Is<RepositoryItem>(i =>
                    string.Equals(i.Name, "repo2", StringComparison.Ordinal) &&
                    string.Equals(i.ServiceIndex, source, StringComparison.OrdinalIgnoreCase) &&
                    i.Owners.Count == 3 &&
                    i.Owners.SequenceEqual(expectedOwners) &&
                    i.Certificates.Count == 1 &&
                    SettingsTestUtils.DeepEquals(i.Certificates.First(), expectedCert))));
        }
    }
}
