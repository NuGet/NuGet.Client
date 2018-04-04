// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class RepositorySignatureInfoUtilityTests
    {
        private static SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.Default();
        private static SignedPackageVerifierSettings _verifyCommandDefaultSettings = SignedPackageVerifierSettings.VerifyCommandDefaultPolicy();

        [Fact]
        public void GetSignedPackageVerifierSettings_NullCommonSettingsThrows()
        {
            // Arrange & Act
            Action action = () => RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(null, null);

            // Assert
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_NullRepoSignatureInfoReturnsCommonSettings()
        {
            // Arrange
            var commonSettings = _defaultSettings;

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(null, commonSettings);

            // Assert
            settings.Should().Be(commonSettings);
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoTrueAllSignedClearsAllowUnsignedIfSet()
        {
            // Arrange
            var commonSettings = _defaultSettings;
            var allSigned = true;
            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, null);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.AllowUnsigned.Should().BeFalse();
            settings.AllowIllegal.Should().BeTrue();
            settings.AllowUntrusted.Should().BeTrue();
            settings.AllowUntrustedSelfIssuedCertificate.Should().BeTrue();
            settings.AllowIgnoreTimestamp.Should().BeTrue();
            settings.AllowNoTimestamp.Should().BeTrue();
            settings.AllowMultipleTimestamps.Should().BeTrue();
            settings.AllowUnknownRevocation.Should().BeTrue();
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoTrueAllSignedClearsAllowUnsignedIfNotSet()
        {
            // Arrange
            var commonSettings = _verifyCommandDefaultSettings;
            var allSigned = true;
            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, null);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.AllowUnsigned.Should().BeFalse();
            settings.AllowIllegal.Should().BeFalse();
            settings.AllowUntrusted.Should().BeFalse();
            settings.AllowUntrustedSelfIssuedCertificate.Should().BeTrue();
            settings.AllowIgnoreTimestamp.Should().BeFalse();
            settings.AllowNoTimestamp.Should().BeTrue();
            settings.AllowMultipleTimestamps.Should().BeTrue();
            settings.AllowUnknownRevocation.Should().BeTrue();
            settings.AllowNoClientCertificateList.Should().BeTrue();
            settings.AllowNoRepositoryCertificateList.Should().BeFalse();
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoFalseAllSignedDoesNotSetAllowUnsigned()
        {
            // Arrange
            var commonSettings = _defaultSettings;
            var allSigned = false;
            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, null);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.ShouldBeEquivalentTo(commonSettings);
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoFalseAllSignedDoesNotClearAllowUnsigned()
        {
            // Arrange
            var commonSettings = _verifyCommandDefaultSettings;
            var allSigned = false;
            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, null);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.ShouldBeEquivalentTo(commonSettings);
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoCertificateListWithOneEntryCorrectlyPassedToSetting()
        {
            // Arrange
            var target = VerificationTarget.Repository;
            var commonSettings = _defaultSettings;
            var allSigned = true;
            var certFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), HashAlgorithmName.SHA256.ToString() },
                { HashAlgorithmName.SHA384.ConvertToOidString(), HashAlgorithmName.SHA384.ToString() },
                { HashAlgorithmName.SHA512.ConvertToOidString(), HashAlgorithmName.SHA512.ToString() },
                { "1.3.14.3.2.26", "SHA1" },
            };

            var testCertInfo = new TestRepositoryCertificateInfo()
            {
                ContentUrl = @"http://unit.test",
                Fingerprints = new Fingerprints(certFingerprints),
                Issuer = "CN=Issuer",
                Subject = "CN=Subject",
                NotBefore = DateTimeOffset.UtcNow,
                NotAfter = DateTimeOffset.UtcNow
            };

            var repoCertificateInfo = new List<IRepositoryCertificateInfo>()
            {
                testCertInfo
            };

            var expectedAllowList = new List<CertificateHashAllowListEntry>()
            {
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA256.ToString(), HashAlgorithmName.SHA256),
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA384.ToString(), HashAlgorithmName.SHA384),
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA512.ToString(), HashAlgorithmName.SHA512)
            };

            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, repoCertificateInfo);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.AllowUnsigned.Should().BeFalse();
            settings.AllowNoClientCertificateList.Should().BeTrue();
            settings.AllowNoRepositoryCertificateList.Should().BeFalse();
            settings.RepositoryCertificateList.Should().NotBeNull();
            settings.RepositoryCertificateList.ShouldBeEquivalentTo(expectedAllowList);
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_RepoSignatureInfoCertificateListWithMultipleEntriesCorrectlyPassedToSetting()
        {
            // Arrange
            var target = VerificationTarget.Repository;
            var commonSettings = _defaultSettings;
            var allSigned = true;
            var firstCertFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), $"{HashAlgorithmName.SHA256.ToString()}_first" },
                { HashAlgorithmName.SHA384.ConvertToOidString(), $"{HashAlgorithmName.SHA384.ToString()}_first" },
                { HashAlgorithmName.SHA512.ConvertToOidString(), $"{HashAlgorithmName.SHA512.ToString()}_first" }
            };

            var secondCertFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), $"{HashAlgorithmName.SHA256.ToString()}_second"},
            };

            var repoCertificateInfo = new List<IRepositoryCertificateInfo>()
            {
                new TestRepositoryCertificateInfo()
                {
                    ContentUrl = @"http://unit.test/1",
                    Fingerprints = new Fingerprints(firstCertFingerprints),
                    Issuer = "CN=Issuer1",
                    Subject = "CN=Subject1",
                    NotBefore = DateTimeOffset.UtcNow,
                    NotAfter = DateTimeOffset.UtcNow
                },
                new TestRepositoryCertificateInfo()
                {
                    ContentUrl = @"http://unit.test/2",
                    Fingerprints = new Fingerprints(secondCertFingerprints),
                    Issuer = "CN=Issuer2",
                    Subject = "CN=Subject2",
                    NotBefore = DateTimeOffset.UtcNow,
                    NotAfter = DateTimeOffset.UtcNow
                }
            };

            var expectedAllowList = new List<CertificateHashAllowListEntry>()
            {
                new CertificateHashAllowListEntry(target, $"{HashAlgorithmName.SHA256.ToString()}_first", HashAlgorithmName.SHA256),
                new CertificateHashAllowListEntry(target, $"{HashAlgorithmName.SHA384.ToString()}_first", HashAlgorithmName.SHA384),
                new CertificateHashAllowListEntry(target, $"{HashAlgorithmName.SHA512.ToString()}_first", HashAlgorithmName.SHA512),
                new CertificateHashAllowListEntry(target, $"{HashAlgorithmName.SHA256.ToString()}_second", HashAlgorithmName.SHA256)
            };

            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, repoCertificateInfo);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.AllowUnsigned.Should().BeFalse();
            settings.AllowNoClientCertificateList.Should().BeTrue();
            settings.AllowNoRepositoryCertificateList.Should().BeFalse();
            settings.RepositoryCertificateList.Should().NotBeNull();
            settings.RepositoryCertificateList.ShouldBeEquivalentTo(expectedAllowList);
        }

        [Fact]
        public void GetSignedPackageVerifierSettings_ClientAllowListInfoPassedToSetting()
        {
            // Arrange
            var target = VerificationTarget.Repository;
            var allSigned = true;
            var certFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), HashAlgorithmName.SHA256.ToString() },
                { HashAlgorithmName.SHA384.ConvertToOidString(), HashAlgorithmName.SHA384.ToString() },
                { HashAlgorithmName.SHA512.ConvertToOidString(), HashAlgorithmName.SHA512.ToString() },
                { "1.3.14.3.2.26", "SHA1" },
            };

            var testCertInfo = new TestRepositoryCertificateInfo()
            {
                ContentUrl = @"http://unit.test",
                Fingerprints = new Fingerprints(certFingerprints),
                Issuer = "CN=Issuer",
                Subject = "CN=Subject",
                NotBefore = DateTimeOffset.UtcNow,
                NotAfter = DateTimeOffset.UtcNow
            };

            var repoCertificateInfo = new List<IRepositoryCertificateInfo>()
            {
                testCertInfo
            };

            var expectedClientAllowList = new List<CertificateHashAllowListEntry>()
            {
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA256.ToString(), HashAlgorithmName.SHA256)
            };


            var expectedRepoAllowList = new List<CertificateHashAllowListEntry>()
            {
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA256.ToString(), HashAlgorithmName.SHA256),
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA384.ToString(), HashAlgorithmName.SHA384),
                new CertificateHashAllowListEntry(target, HashAlgorithmName.SHA512.ToString(), HashAlgorithmName.SHA512)
            };

            var repoSignatureInfo = new RepositorySignatureInfo(allSigned, repoCertificateInfo);

            var commonSettings = new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: false,
                repoAllowListEntries: null,
                clientAllowListEntries: expectedClientAllowList);

            // Act
            var settings = RepositorySignatureInfoUtility.GetSignedPackageVerifierSettings(repoSignatureInfo, commonSettings);

            // Assert
            settings.AllowUnsigned.Should().BeFalse();
            settings.AllowNoClientCertificateList.Should().BeFalse();
            settings.AllowNoRepositoryCertificateList.Should().BeFalse();
            settings.ClientCertificateList.Should().NotBeNull();
            settings.ClientCertificateList.ShouldBeEquivalentTo(expectedClientAllowList);
            settings.RepositoryCertificateList.Should().NotBeNull();
            settings.RepositoryCertificateList.ShouldBeEquivalentTo(expectedRepoAllowList);
        }
    }
}
