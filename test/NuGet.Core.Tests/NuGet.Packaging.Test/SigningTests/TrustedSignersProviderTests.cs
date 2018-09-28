// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Configuration.Test;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class TrustedSignersProviderTests
    {
        [Fact]
        public void Constructor_WithNullArgument_Throws()
        {
            // Act and Assert
            var ex = Record.Exception(() => new TrustedSignersProvider(settings: null));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void GetAllowListEntries_WithoutTrustedSignersSection_ReturnsEmptyList()
        {
            // Arrange
            var config = @"
<configuration>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Should().BeEmpty();
            }
        }

        [Fact]
        public void GetAllowListEntries_WithEmptyTrustedSignersSection_ReturnsEmptyList()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Should().BeEmpty();
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedAuthor_WithOneCertificate_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <author name=""author1"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
        <author name=""author2"">
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(2);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "abc", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "def", HashAlgorithmName.SHA256)
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedAuthor_WithMultipleCertificates_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <author name=""author1"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""ghi"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
        <author name=""author2"">
            <certificate fingerprint=""jkl"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""mno"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(5);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "abc", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "def", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "ghi", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "jkl", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "mno", HashAlgorithmName.SHA256)
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedRepository_WithOneCertificate_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <repository name=""repository1"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </repository>
        <repository name=""repository2"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </repository>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(2);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new TrustedRepositoryAllowListEntry("abc", HashAlgorithmName.SHA256, owners: null),
                    new TrustedRepositoryAllowListEntry("def", HashAlgorithmName.SHA256, owners: null)
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedRepository_WithMultipleCertificates_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <repository name=""repository1"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""ghi"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </repository>
        <repository name=""repository2"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""jkl"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""mno"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </repository>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(5);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new TrustedRepositoryAllowListEntry("abc", HashAlgorithmName.SHA256, owners: null),
                    new TrustedRepositoryAllowListEntry("def", HashAlgorithmName.SHA256, owners: null),
                    new TrustedRepositoryAllowListEntry("ghi", HashAlgorithmName.SHA256, owners: null),
                    new TrustedRepositoryAllowListEntry("jkl", HashAlgorithmName.SHA256, owners: null),
                    new TrustedRepositoryAllowListEntry("mno", HashAlgorithmName.SHA256, owners: null)
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedRepository_WithMultipleCertificates_WithOwners_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <repository name=""repository1"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""ghi"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <owners>nuget;randomOwner</owners>
        </repository>
        <repository name=""repository2"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""jkl"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""mno"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <owners>anotherOwner;owner3</owners>
        </repository>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(5);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new TrustedRepositoryAllowListEntry("abc", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new TrustedRepositoryAllowListEntry("def", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new TrustedRepositoryAllowListEntry("ghi", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new TrustedRepositoryAllowListEntry("jkl", HashAlgorithmName.SHA256, owners: new List<string>() { "anotherOwner", "owner3" }),
                    new TrustedRepositoryAllowListEntry("mno", HashAlgorithmName.SHA256, owners: new List<string>() { "anotherOwner", "owner3" })
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }

        [Fact]
        public void GetAllowListEntries_TrustedAuthorsAndRepositories_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <trustedSigners>
        <repository name=""repository1"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""def"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""ghi"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <owners>nuget;randomOwner</owners>
        </repository>
        <author name=""author1"">
            <certificate fingerprint=""xyz"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
        <author name=""author2"">
            <certificate fingerprint=""pqr"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""stu"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
        <repository name=""repository2"" serviceIndex=""api.v3ServiceIndex.test/json"">
            <certificate fingerprint=""jkl"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""mno"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <owners>anotherOwner;owner3</owners>
        </repository>
    </trustedSigners>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var trustedSignersProvider = new TrustedSignersProvider(settings);
                var entries = trustedSignersProvider.GetAllowListEntries();
                entries.Should().NotBeNull();
                entries.Count.Should().Be(8);

                var expectedEntries = new List<VerificationAllowListEntry>()
                {
                    new TrustedRepositoryAllowListEntry("abc", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new TrustedRepositoryAllowListEntry("def", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new TrustedRepositoryAllowListEntry("ghi", HashAlgorithmName.SHA256, owners: new List<string>() { "nuget", "randomOwner" }),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "xyz", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "pqr", HashAlgorithmName.SHA256),
                    new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "stu", HashAlgorithmName.SHA256),
                    new TrustedRepositoryAllowListEntry("jkl", HashAlgorithmName.SHA256, owners: new List<string>() { "anotherOwner", "owner3" }),
                    new TrustedRepositoryAllowListEntry("mno", HashAlgorithmName.SHA256, owners: new List<string>() { "anotherOwner", "owner3" })
                };

                entries.Should().BeEquivalentTo(expectedEntries);
            }
        }
    }
}
