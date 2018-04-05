// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedPackageVerifierSettingsTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConstructorWithoutLists_InitializesProperties(bool initialValue)
        {
            // Arrange & Act
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: initialValue,
                allowIllegal: initialValue,
                allowUntrusted: initialValue,
                allowUntrustedSelfIssuedCertificate: initialValue,
                allowIgnoreTimestamp: initialValue,
                allowMultipleTimestamps: initialValue,
                allowNoTimestamp: initialValue,
                allowUnknownRevocation: initialValue,
                allowNoRepositoryCertificateList: initialValue,
                allowNoClientCertificateList: initialValue);

            // Assert
            settings.AllowUnsigned.Should().Be(initialValue);
            settings.AllowIllegal.Should().Be(initialValue);
            settings.AllowUntrusted.Should().Be(initialValue);
            settings.AllowUntrustedSelfIssuedCertificate.Should().Be(initialValue);
            settings.AllowIgnoreTimestamp.Should().Be(initialValue);
            settings.AllowMultipleTimestamps.Should().Be(initialValue);
            settings.AllowNoTimestamp.Should().Be(initialValue);
            settings.AllowUnknownRevocation.Should().Be(initialValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(initialValue);
            settings.AllowNoClientCertificateList.Should().Be(initialValue);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConstructorWithLists_InitializesProperties(bool initialValue)
        {
            // Arrange
            var repoList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();

            // Act
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: initialValue,
                allowIllegal: initialValue,
                allowUntrusted: initialValue,
                allowUntrustedSelfIssuedCertificate: initialValue,
                allowIgnoreTimestamp: initialValue,
                allowMultipleTimestamps: initialValue,
                allowNoTimestamp: initialValue,
                allowUnknownRevocation: initialValue,
                allowNoRepositoryCertificateList: initialValue,
                allowNoClientCertificateList: initialValue,
                repoAllowListEntries: repoList,
                clientAllowListEntries: clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(initialValue);
            settings.AllowIllegal.Should().Be(initialValue);
            settings.AllowUntrusted.Should().Be(initialValue);
            settings.AllowUntrustedSelfIssuedCertificate.Should().Be(initialValue);
            settings.AllowIgnoreTimestamp.Should().Be(initialValue);
            settings.AllowMultipleTimestamps.Should().Be(initialValue);
            settings.AllowNoTimestamp.Should().Be(initialValue);
            settings.AllowUnknownRevocation.Should().Be(initialValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(initialValue);
            settings.AllowNoClientCertificateList.Should().Be(initialValue);
            settings.RepositoryCertificateList.Should().BeSameAs(repoList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);
        }

        [Fact]
        public void GetDefault_InitializesProperties()
        {
            // Arrange
            var repoList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();
            var defaultValue = true;

            // Act
            var settings = SignedPackageVerifierSettings.GetDefault(repoList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(defaultValue);
            settings.AllowIllegal.Should().Be(defaultValue);
            settings.AllowUntrusted.Should().Be(defaultValue);
            settings.AllowUntrustedSelfIssuedCertificate.Should().Be(defaultValue);
            settings.AllowIgnoreTimestamp.Should().Be(defaultValue);
            settings.AllowMultipleTimestamps.Should().Be(defaultValue);
            settings.AllowNoTimestamp.Should().Be(defaultValue);
            settings.AllowUnknownRevocation.Should().Be(defaultValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(defaultValue);
            settings.AllowNoClientCertificateList.Should().Be(defaultValue);
            settings.RepositoryCertificateList.Should().BeSameAs(repoList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);
        }

        [Fact]
        public void GetVSClientDefaultPolicy_InitializesProperties()
        {
            // Arrange
            var repoList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();
            var defaultValue = true;

            // Act
            var settings = SignedPackageVerifierSettings.GetVSClientDefaultPolicy(repoList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(defaultValue);
            settings.AllowIllegal.Should().Be(defaultValue);
            settings.AllowUntrusted.Should().Be(defaultValue);
            settings.AllowUntrustedSelfIssuedCertificate.Should().Be(defaultValue);
            settings.AllowIgnoreTimestamp.Should().Be(defaultValue);
            settings.AllowMultipleTimestamps.Should().Be(defaultValue);
            settings.AllowNoTimestamp.Should().Be(defaultValue);
            settings.AllowUnknownRevocation.Should().Be(defaultValue);
            settings.AllowNoRepositoryCertificateList.Should().Be(defaultValue);
            settings.AllowNoClientCertificateList.Should().Be(defaultValue);
            settings.RepositoryCertificateList.Should().BeSameAs(repoList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);
        }

        [Fact]
        public void GetVerifyCommandDefaultPolicy_InitializesProperties()
        {
            // Arrange
            var repoList = new List<CertificateHashAllowListEntry>();
            var clientList = new List<CertificateHashAllowListEntry>();

            // Act
            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(repoList, clientList);

            // Assert
            settings.AllowUnsigned.Should().Be(false);
            settings.AllowIllegal.Should().Be(false);
            settings.AllowUntrusted.Should().Be(false);
            settings.AllowUntrustedSelfIssuedCertificate.Should().Be(true);
            settings.AllowIgnoreTimestamp.Should().Be(false);
            settings.AllowMultipleTimestamps.Should().Be(true);
            settings.AllowNoTimestamp.Should().Be(true);
            settings.AllowUnknownRevocation.Should().Be(true);
            settings.AllowNoRepositoryCertificateList.Should().Be(true);
            settings.AllowNoClientCertificateList.Should().Be(true);
            settings.RepositoryCertificateList.Should().BeSameAs(repoList);
            settings.ClientCertificateList.Should().BeSameAs(clientList);
        }
    }
}
