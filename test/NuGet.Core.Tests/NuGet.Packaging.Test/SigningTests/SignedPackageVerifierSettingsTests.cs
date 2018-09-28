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
    public class SignedPackageVerifierSettingsTests
    {
        const string RevocationModeEnvVar = "NUGET_CERT_REVOCATION_MODE";

        [Fact]
        public void ConstructorWithoutLists_WhenVerificationTargetIsUnrecognized_Throws()
        {
            var verificationTarget = (VerificationTarget)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: verificationTarget,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online));

            Assert.Equal("verificationTarget", exception.ParamName);
            Assert.StartsWith($"The enum value '{verificationTarget}' is unrecognized.", exception.Message);
        }

        [Fact]
        public void ConstructorWithoutLists_WhenSignaturePlacementIsUnrecognized_Throws()
        {
            var signaturePlacement = (SignaturePlacement)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online));

            Assert.Equal("signaturePlacement", exception.ParamName);
            Assert.StartsWith($"The enum value '{signaturePlacement}' is unrecognized.", exception.Message);
        }

        [Fact]
        public void ConstructorWithoutLists_WhenRepositoryCountersignatureVerificationBehaviorIsUnrecognized_Throws()
        {
            var repositoryCountersignatureVerificationBehavior = (SignatureVerificationBehavior)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: repositoryCountersignatureVerificationBehavior,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online));

            Assert.Equal("repositoryCountersignatureVerificationBehavior", exception.ParamName);
            Assert.StartsWith($"The enum value '{repositoryCountersignatureVerificationBehavior}' is unrecognized.", exception.Message);
        }

        [Theory]
        [InlineData(
            VerificationTarget.Author,
            SignaturePlacement.Countersignature,
            SignatureVerificationBehavior.IfExists,
            "verificationTarget",
            "signaturePlacement")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.Countersignature,
            SignatureVerificationBehavior.IfExists,
            "verificationTarget",
            "signaturePlacement")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.Any,
            SignatureVerificationBehavior.Never,
            "signaturePlacement",
            "repositoryCountersignatureVerificationBehavior")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.PrimarySignature,
            SignatureVerificationBehavior.IfExists,
            "signaturePlacement",
            "repositoryCountersignatureVerificationBehavior")]
        public void ConstructorWithoutLists_WhenArgumentCombinationIsInvalid_Throws(
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior repositoryCountersignatureVerificationBehavior,
            string parameterName1,
            string parameterName2)
        {
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: verificationTarget,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: repositoryCountersignatureVerificationBehavior,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online));

            Assert.Equal(parameterName2, exception.ParamName);
            Assert.StartsWith($"Invalid combination of arguments {parameterName1} and {parameterName2}.", exception.Message);
        }

        [Theory]
        [InlineData(true, VerificationTarget.Author, SignaturePlacement.PrimarySignature, SignatureVerificationBehavior.Never, RevocationMode.Online)]
        [InlineData(false, VerificationTarget.All, SignaturePlacement.Any, SignatureVerificationBehavior.IfExistsAndIsNecessary, RevocationMode.Offline)]
        public void ConstructorWithoutLists_InitializesProperties(
            bool boolValue,
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior signatureVerificationBehavior,
            RevocationMode revocationMode)
        {
            // Arrange & Act
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: boolValue,
                allowIllegal: boolValue,
                allowUntrusted: boolValue,
                allowIgnoreTimestamp: boolValue,
                allowMultipleTimestamps: boolValue,
                allowNoTimestamp: boolValue,
                allowUnknownRevocation: boolValue,
                reportUnknownRevocation: boolValue,
                verificationTarget: verificationTarget,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: signatureVerificationBehavior,
                allowNoRepositoryCertificateList: boolValue,
                allowNoClientCertificateList: boolValue,
                revocationMode: revocationMode);

            // Assert
            settings.AllowUnsigned.Should().Be(boolValue);
            settings.AllowIllegal.Should().Be(boolValue);
            settings.AllowUntrusted.Should().Be(boolValue);
            settings.AllowIgnoreTimestamp.Should().Be(boolValue);
            settings.AllowMultipleTimestamps.Should().Be(boolValue);
            settings.AllowNoTimestamp.Should().Be(boolValue);
            settings.AllowUnknownRevocation.Should().Be(boolValue);
            settings.ReportUnknownRevocation.Should().Be(boolValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(boolValue);
            settings.AllowNoClientCertificateList.Should().Be(boolValue);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(signatureVerificationBehavior);
            settings.RevocationMode.Should().Be(revocationMode);
        }

        [Fact]
        public void ConstructorWithLists_WhenVerificationTargetIsUnrecognized_Throws()
        {
            var verificationTarget = (VerificationTarget)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: verificationTarget,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online,
                allowListEntries: Array.Empty<VerificationAllowListEntry>(),
                clientAllowListEntries: Array.Empty<VerificationAllowListEntry>()));

            Assert.Equal("verificationTarget", exception.ParamName);
            Assert.StartsWith($"The enum value '{verificationTarget}' is unrecognized.", exception.Message);
        }

        [Fact]
        public void ConstructorWithLists_WhenSignaturePlacementIsUnrecognized_Throws()
        {
            var signaturePlacement = (SignaturePlacement)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online,
                allowListEntries: Array.Empty<VerificationAllowListEntry>(),
                clientAllowListEntries: Array.Empty<VerificationAllowListEntry>()));

            Assert.Equal("signaturePlacement", exception.ParamName);
            Assert.StartsWith($"The enum value '{signaturePlacement}' is unrecognized.", exception.Message);
        }

        [Fact]
        public void ConstructorWithLists_WhenRepositoryCountersignatureVerificationBehaviorIsUnrecognized_Throws()
        {
            var repositoryCountersignatureVerificationBehavior = (SignatureVerificationBehavior)int.MaxValue;
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: repositoryCountersignatureVerificationBehavior,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online,
                allowListEntries: Array.Empty<VerificationAllowListEntry>(),
                clientAllowListEntries: Array.Empty<VerificationAllowListEntry>()));

            Assert.Equal("repositoryCountersignatureVerificationBehavior", exception.ParamName);
            Assert.StartsWith($"The enum value '{repositoryCountersignatureVerificationBehavior}' is unrecognized.", exception.Message);
        }

        [Theory]
        [InlineData(
            VerificationTarget.Author,
            SignaturePlacement.Countersignature,
            SignatureVerificationBehavior.IfExists,
            "verificationTarget",
            "signaturePlacement")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.Countersignature,
            SignatureVerificationBehavior.IfExists,
            "verificationTarget",
            "signaturePlacement")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.Any,
            SignatureVerificationBehavior.Never,
            "signaturePlacement",
            "repositoryCountersignatureVerificationBehavior")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.PrimarySignature,
            SignatureVerificationBehavior.Always,
            "signaturePlacement",
            "repositoryCountersignatureVerificationBehavior")]
        [InlineData(
            VerificationTarget.All,
            SignaturePlacement.PrimarySignature,
            SignatureVerificationBehavior.IfExists,
            "signaturePlacement",
            "repositoryCountersignatureVerificationBehavior")]
        public void ConstructorWithLists_WhenArgumentCombinationIsInvalid_Throws(
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior repositoryCountersignatureVerificationBehavior,
            string parameterName1,
            string parameterName2)
        {
            var exception = Assert.Throws<ArgumentException>(() => new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: verificationTarget,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: repositoryCountersignatureVerificationBehavior,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                revocationMode: RevocationMode.Online,
                allowListEntries: Array.Empty<VerificationAllowListEntry>(),
                clientAllowListEntries: Array.Empty<VerificationAllowListEntry>()));

            Assert.Equal(parameterName2, exception.ParamName);
            Assert.StartsWith($"Invalid combination of arguments {parameterName1} and {parameterName2}.", exception.Message);
        }

        [Theory]
        [InlineData(true, VerificationTarget.Unknown, SignaturePlacement.PrimarySignature, SignatureVerificationBehavior.Never, RevocationMode.Online)]
        [InlineData(false, VerificationTarget.All, SignaturePlacement.Any, SignatureVerificationBehavior.IfExistsAndIsNecessary, RevocationMode.Offline)]
        public void ConstructorWithLists_InitializesProperties(
            bool boolValue,
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior signatureVerificationBehavior,
            RevocationMode revocationMode)
        {
            // Arrange
            var allowList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();

            // Act
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: boolValue,
                allowIllegal: boolValue,
                allowUntrusted: boolValue,
                allowIgnoreTimestamp: boolValue,
                allowMultipleTimestamps: boolValue,
                allowNoTimestamp: boolValue,
                allowUnknownRevocation: boolValue,
                reportUnknownRevocation: boolValue,
                allowNoRepositoryCertificateList: boolValue,
                allowNoClientCertificateList: boolValue,
                verificationTarget: verificationTarget,
                signaturePlacement: signaturePlacement,
                repositoryCountersignatureVerificationBehavior: signatureVerificationBehavior,
                revocationMode: revocationMode,
                allowListEntries: allowList,
                clientAllowListEntries: clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(boolValue);
            settings.AllowIllegal.Should().Be(boolValue);
            settings.AllowUntrusted.Should().Be(boolValue);
            settings.AllowIgnoreTimestamp.Should().Be(boolValue);
            settings.AllowMultipleTimestamps.Should().Be(boolValue);
            settings.AllowNoTimestamp.Should().Be(boolValue);
            settings.AllowUnknownRevocation.Should().Be(boolValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(boolValue);
            settings.AllowNoClientCertificateList.Should().Be(boolValue);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(signatureVerificationBehavior);
            settings.RevocationMode.Should().Be(revocationMode);
            settings.AllowList.Should().BeSameAs(allowList);
        }

        [Theory]
        [InlineData(null, RevocationMode.Online)]
        [InlineData("", RevocationMode.Online)]
        [InlineData("online", RevocationMode.Online)]
        [InlineData("offline", RevocationMode.Offline)]
        [InlineData("Offline", RevocationMode.Offline)]
        public void GetDefault_InitializesProperties(string revocationModeEnvVar, RevocationMode expectedRevocationMode)
        {
            // Arrange
            var allowList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();
            var defaultValue = true;

            if (revocationModeEnvVar != null)
            {
                Environment.SetEnvironmentVariable(RevocationModeEnvVar, revocationModeEnvVar);
            }

            // Act
            var settings = SignedPackageVerifierSettings.GetDefault(allowList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(defaultValue);
            settings.AllowIllegal.Should().Be(defaultValue);
            settings.AllowUntrusted.Should().Be(defaultValue);
            settings.AllowIgnoreTimestamp.Should().Be(defaultValue);
            settings.AllowMultipleTimestamps.Should().Be(defaultValue);
            settings.AllowNoTimestamp.Should().Be(defaultValue);
            settings.AllowUnknownRevocation.Should().Be(defaultValue);
            settings.ReportUnknownRevocation.Should().Be(false);
            settings.AllowNoRepositoryCertificateList.Should().Be(defaultValue);
            settings.AllowNoClientCertificateList.Should().Be(defaultValue);
            settings.VerificationTarget.Should().Be(VerificationTarget.All);
            settings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
            settings.RevocationMode.Should().Be(expectedRevocationMode);
            settings.AllowList.Should().BeSameAs(allowList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);

            Environment.SetEnvironmentVariable(RevocationModeEnvVar, string.Empty);
        }

        [Theory]
        [InlineData(null, RevocationMode.Online)]
        [InlineData("", RevocationMode.Online)]
        [InlineData("online", RevocationMode.Online)]
        [InlineData("offline", RevocationMode.Offline)]
        [InlineData("Offline", RevocationMode.Offline)]
        public void GetAcceptModeDefaultPolicy_InitializesProperties(string revocationModeEnvVar, RevocationMode expectedRevocationMode)
        {
            // Arrange
            var allowList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();
            var defaultValue = true;

            if (revocationModeEnvVar != null)
            {
                Environment.SetEnvironmentVariable(RevocationModeEnvVar, revocationModeEnvVar);
            }

            // Act
            var settings = SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy(allowList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(defaultValue);
            settings.AllowIllegal.Should().Be(defaultValue);
            settings.AllowUntrusted.Should().Be(defaultValue);
            settings.AllowIgnoreTimestamp.Should().Be(defaultValue);
            settings.AllowMultipleTimestamps.Should().Be(defaultValue);
            settings.AllowNoTimestamp.Should().Be(defaultValue);
            settings.AllowUnknownRevocation.Should().Be(defaultValue);
            settings.ReportUnknownRevocation.Should().Be(false);
            settings.AllowNoRepositoryCertificateList.Should().Be(defaultValue);
            settings.AllowNoClientCertificateList.Should().Be(defaultValue);
            settings.VerificationTarget.Should().Be(VerificationTarget.All);
            settings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
            settings.RevocationMode.Should().Be(expectedRevocationMode);
            settings.AllowList.Should().BeSameAs(allowList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);

            Environment.SetEnvironmentVariable(RevocationModeEnvVar, string.Empty);
        }

        [Theory]
        [InlineData(null, RevocationMode.Online)]
        [InlineData("", RevocationMode.Online)]
        [InlineData("online", RevocationMode.Online)]
        [InlineData("offline", RevocationMode.Offline)]
        [InlineData("Offline", RevocationMode.Offline)]
        public void GetRequireModeDefaultPolicy_InitializesProperties(string revocationModeEnvVar, RevocationMode expectedRevocationMode)
        {
            // Arrange
            var allowList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();

            if (revocationModeEnvVar != null)
            {
                Environment.SetEnvironmentVariable(RevocationModeEnvVar, revocationModeEnvVar);
            }

            // Act
            var settings = SignedPackageVerifierSettings.GetRequireModeDefaultPolicy(allowList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(false);
            settings.AllowIllegal.Should().Be(false);
            settings.AllowUntrusted.Should().Be(false);
            settings.AllowIgnoreTimestamp.Should().Be(true);
            settings.AllowMultipleTimestamps.Should().Be(true);
            settings.AllowNoTimestamp.Should().Be(true);
            settings.AllowUnknownRevocation.Should().Be(true);
            settings.ReportUnknownRevocation.Should().Be(true);
            settings.AllowNoRepositoryCertificateList.Should().Be(true);
            settings.AllowNoClientCertificateList.Should().Be(false);
            settings.VerificationTarget.Should().Be(VerificationTarget.All);
            settings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
            settings.RevocationMode.Should().Be(expectedRevocationMode);
            settings.AllowList.Should().BeSameAs(allowList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);

            Environment.SetEnvironmentVariable(RevocationModeEnvVar, string.Empty);
        }

        [Theory]
        [InlineData(null, RevocationMode.Online)]
        [InlineData("", RevocationMode.Online)]
        [InlineData("online", RevocationMode.Online)]
        [InlineData("offline", RevocationMode.Offline)]
        [InlineData("Offline", RevocationMode.Offline)]
        public void GetVerifyCommandDefaultPolicy_InitializesProperties(string revocationModeEnvVar, RevocationMode expectedRevocationMode)
        {
            // Arrange
            var allowList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();

            if (revocationModeEnvVar != null)
            {
                Environment.SetEnvironmentVariable(RevocationModeEnvVar, revocationModeEnvVar);
            }

            // Act
            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(allowList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(false);
            settings.AllowIllegal.Should().Be(false);
            settings.AllowUntrusted.Should().Be(false);
            settings.AllowIgnoreTimestamp.Should().Be(false);
            settings.AllowMultipleTimestamps.Should().Be(true);
            settings.AllowNoTimestamp.Should().Be(true);
            settings.AllowUnknownRevocation.Should().Be(true);
            settings.ReportUnknownRevocation.Should().Be(true);
            settings.AllowNoRepositoryCertificateList.Should().Be(true);
            settings.AllowNoClientCertificateList.Should().Be(true);
            settings.VerificationTarget.Should().Be(VerificationTarget.All);
            settings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
            settings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExists);
            settings.RevocationMode.Should().Be(expectedRevocationMode);
            settings.AllowList.Should().BeSameAs(allowList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);

            Environment.SetEnvironmentVariable(RevocationModeEnvVar, string.Empty);
        }

        [Fact]
        public void GetClientPolicy_WhenNoClientPolicy_DefaultsToAccept()
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

                var verifierSettings = SignedPackageVerifierSettings.GetClientPolicy(settings, NullLogger.Instance);

                verifierSettings.AllowUnsigned.Should().Be(true);
                verifierSettings.AllowIllegal.Should().Be(true);
                verifierSettings.AllowUntrusted.Should().Be(true);
                verifierSettings.AllowIgnoreTimestamp.Should().Be(true);
                verifierSettings.AllowMultipleTimestamps.Should().Be(true);
                verifierSettings.AllowNoTimestamp.Should().Be(true);
                verifierSettings.AllowUnknownRevocation.Should().Be(true);
                verifierSettings.ReportUnknownRevocation.Should().Be(false);
                verifierSettings.AllowNoRepositoryCertificateList.Should().Be(true);
                verifierSettings.AllowNoClientCertificateList.Should().Be(true);
                verifierSettings.VerificationTarget.Should().Be(VerificationTarget.All);
                verifierSettings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
                verifierSettings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
                verifierSettings.RevocationMode.Should().Be(RevocationMode.Online);
                verifierSettings.RepositoryCertificateList.Should().BeNull();
                verifierSettings.ClientCertificateList.Should().BeEmpty();
            }
        }

        [Fact]
        public void GetClientPolicy_AcceptMode_ReadsClientPolicyCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <config>
        <add key=""signatureValidationMode"" value=""accept"" />
    </config>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var verifierSettings = SignedPackageVerifierSettings.GetClientPolicy(settings, NullLogger.Instance);

                verifierSettings.AllowUnsigned.Should().Be(true);
                verifierSettings.AllowIllegal.Should().Be(true);
                verifierSettings.AllowUntrusted.Should().Be(true);
                verifierSettings.AllowIgnoreTimestamp.Should().Be(true);
                verifierSettings.AllowMultipleTimestamps.Should().Be(true);
                verifierSettings.AllowNoTimestamp.Should().Be(true);
                verifierSettings.AllowUnknownRevocation.Should().Be(true);
                verifierSettings.ReportUnknownRevocation.Should().Be(false);
                verifierSettings.AllowNoRepositoryCertificateList.Should().Be(true);
                verifierSettings.AllowNoClientCertificateList.Should().Be(true);
                verifierSettings.VerificationTarget.Should().Be(VerificationTarget.All);
                verifierSettings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
                verifierSettings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
                verifierSettings.RevocationMode.Should().Be(RevocationMode.Online);
                verifierSettings.RepositoryCertificateList.Should().BeNull();
                verifierSettings.ClientCertificateList.Should().BeEmpty();
            }
        }

        [Fact]
        public void GetClientPolicy_RequireMode_ReadsClientPolicyCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <config>
        <add key=""signatureValidationMode"" value=""require"" />
    </config>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settings = new Settings(mockBaseDirectory);
                settings.Should().NotBeNull();

                var verifierSettings = SignedPackageVerifierSettings.GetClientPolicy(settings, NullLogger.Instance);

                verifierSettings.AllowUnsigned.Should().Be(false);
                verifierSettings.AllowIllegal.Should().Be(false);
                verifierSettings.AllowUntrusted.Should().Be(false);
                verifierSettings.AllowIgnoreTimestamp.Should().Be(true);
                verifierSettings.AllowMultipleTimestamps.Should().Be(true);
                verifierSettings.AllowNoTimestamp.Should().Be(true);
                verifierSettings.AllowUnknownRevocation.Should().Be(true);
                verifierSettings.ReportUnknownRevocation.Should().Be(true);
                verifierSettings.AllowNoRepositoryCertificateList.Should().Be(true);
                verifierSettings.AllowNoClientCertificateList.Should().Be(false);
                verifierSettings.VerificationTarget.Should().Be(VerificationTarget.All);
                verifierSettings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
                verifierSettings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
                verifierSettings.RevocationMode.Should().Be(RevocationMode.Online);
                verifierSettings.RepositoryCertificateList.Should().BeNull();
                verifierSettings.ClientCertificateList.Should().BeEmpty();
            }
        }

        [Fact]
        public void GetClientPolicy_ReadsAndParsesTrustedSigners()
        {
            // Arrange
            var config = @"
<configuration>
    <config>
        <add key=""signatureValidationMode"" value=""require"" />
    </config>
    <trustedSigners>
        <author name=""author1"">
            <certificate fingerprint=""abc"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
        <repository name=""repository1"" serviceIndex=""https://v3serviceIndex.test/api/json"">
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

                var expectedAllowList = new List<VerificationAllowListEntry>()
                {
                    new TrustedSignerAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, "abc", HashAlgorithmName.SHA256),
                    new TrustedSignerAllowListEntry(VerificationTarget.Repository, SignaturePlacement.Any,"def", HashAlgorithmName.SHA256)
                };

                var verifierSettings = SignedPackageVerifierSettings.GetClientPolicy(settings, NullLogger.Instance);

                verifierSettings.AllowUnsigned.Should().Be(false);
                verifierSettings.AllowIllegal.Should().Be(false);
                verifierSettings.AllowUntrusted.Should().Be(false);
                verifierSettings.AllowIgnoreTimestamp.Should().Be(true);
                verifierSettings.AllowMultipleTimestamps.Should().Be(true);
                verifierSettings.AllowNoTimestamp.Should().Be(true);
                verifierSettings.AllowUnknownRevocation.Should().Be(true);
                verifierSettings.ReportUnknownRevocation.Should().Be(true);
                verifierSettings.AllowNoRepositoryCertificateList.Should().Be(true);
                verifierSettings.AllowNoClientCertificateList.Should().Be(false);
                verifierSettings.VerificationTarget.Should().Be(VerificationTarget.All);
                verifierSettings.SignaturePlacement.Should().Be(SignaturePlacement.Any);
                verifierSettings.RepositoryCountersignatureVerificationBehavior.Should().Be(SignatureVerificationBehavior.IfExistsAndIsNecessary);
                verifierSettings.RevocationMode.Should().Be(RevocationMode.Online);
                verifierSettings.RepositoryCertificateList.Should().BeNull();
                verifierSettings.ClientCertificateList.Should().BeEquivalentTo(expectedAllowList);
            }
        }
    }
}