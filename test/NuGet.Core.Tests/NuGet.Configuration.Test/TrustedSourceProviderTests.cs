// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class TrustedSourceProviderTests
    {
        [Fact]
        public void ReturnsTrustedSourceForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
        </nuget.org>
    </trustedSources>
</configuration>
";
            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSource = trustedSourceProvider.LoadTrustedSource("nuget.org");

                // Assert
                trustedSource.Should().NotBeNull();
                trustedSource.SourceName.Should().Be("nuget.org");
                trustedSource.ServiceIndex.Should().BeNull();
                trustedSource.Certificates.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void ReturnsTrustedSourceWithMultipleCertificatesForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
            <add key=""HASH2"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA512"" />
        </nuget.org>
    </trustedSources>
</configuration>
";
            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME", HashAlgorithmName.SHA512)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSource = trustedSourceProvider.LoadTrustedSource("nuget.org");

                // Assert
                trustedSource.Should().NotBeNull();
                trustedSource.SourceName.Should().Be("nuget.org");
                trustedSource.ServiceIndex.Should().BeNull();
                trustedSource.Certificates.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void ReturnsNullTrustedSourceForPackageSourceIfAbsent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
        </nuget.org>
    </trustedSources>
</configuration>
";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSource = trustedSourceProvider.LoadTrustedSource("test.org");

                // Assert
                trustedSource.Should().BeNull();
            }
        }

        [Fact]
        public void ReturnsNullTrustedSourceForPackageSourceIfEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
        </nuget.org>
    </trustedSources>
</configuration>
";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSource = trustedSourceProvider.LoadTrustedSource("nuget.org");

                // Assert
                trustedSource.Should().BeNull();
            }
        }

        [Fact]
        public void ReturnsNullTrustedSourceForPackageSourceIfCleared()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
            <clear />
        </nuget.org>
    </trustedSources>
</configuration>
";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSource = trustedSourceProvider.LoadTrustedSource("nuget.org");

                // Assert
                trustedSource.Should().BeNull();
            }
        }

        [Fact]
        public void ReturnsAllTrustedSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
            <add key=""HASH512"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA512"" />
        </nuget.org>
        <test.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA512"" />
        </test.org>
    </trustedSources>
</configuration>
";
            var trustedSource1 = new TrustedSource("nuget.org");
            trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
            trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH512", "SUBJECT_NAME", HashAlgorithmName.SHA512));
            var trustedSource2 = new TrustedSource("test.org");
            trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA512));

            var expectedValues = new List<TrustedSource>
            {
                trustedSource1,
                trustedSource2
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSources = trustedSourceProvider.LoadTrustedSources();

                // Assert
                trustedSources.Should().NotBeNull();
                trustedSources.ShouldBeEquivalentTo(expectedValues);
            }
        }
    }
}
