// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class TrustedSourceProviderTests
    {
        [Fact]
        public void LoadsTrustedSourceForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

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
        public void LoadsTrustedSourceForPackageSourceIfPresentFromNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var config2 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockBaseDirectory, nugetConfigPath), Path.Combine(mockChildDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
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
        public void LoadsAllTrustedSourceIfPresentFromNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var config2 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockBaseDirectory, nugetConfigPath), Path.Combine(mockChildDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                var trustedSources = trustedSourceProvider.LoadTrustedSources().ToList();

                // Assert
                trustedSources.Should().NotBeNull();
                trustedSources.Count.Should().Be(1);
                trustedSources[0].SourceName.Should().Be("nuget.org");
                trustedSources[0].ServiceIndex.Should().BeNull();
                trustedSources[0].Certificates.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void LoadsTrustedSourceWithMultipleCertificatesForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
            <add key='HASH2' value='SUBJECT_NAME' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

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
        public void LoadsTrustedSourceWithDedupedMultipleCertificatesForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
            <add key='HASH' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA384' />
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512)
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
        public void LoadsNullTrustedSourceForPackageSourceIfAbsent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

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
        public void LoadsNullTrustedSourceForPackageSourceIfEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
    </packageSources>
    <trustedSources>
        <nuget.org>
        </nuget.org>
    </trustedSources>
</configuration>";

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
        public void LoadsNullTrustedSourceForPackageSourceIfCleared()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
            <clear />
        </nuget.org>
    </trustedSources>
</configuration>";

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
        public void LoadsTrustedSourceForPackageSourceAfterCleared()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
            <clear />
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA256),
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
        public void LoadsAllTrustedSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
            <add key='HASH512' value='SUBJECT_NAME' fingerprintAlgorithm='SHA512' />
        </nuget.org>
        <test.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA512' />
        </test.org>
    </trustedSources>
</configuration>";

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

        [Fact]
        public void WritesNewTrustedSource()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void WritesUpdatedTrustedSourceWithReplacedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
    </nuget.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA384"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void WritesUpdatedTrustedSourceWithAddedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
    </nuget.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA384"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavesNewTrustedSourceWithOneCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }

        [Fact]
        public void SavesNewTrustedSourceWithMultipleCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key='nuget.org' value='https://nuget.org' />
  </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceWithReplacedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key='nuget.org' value='https://nuget.org' />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
    </nuget.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceWithAddedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key='nuget.org' value='https://nuget.org' />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
    </nuget.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }

        [Fact]
        public void SavesMultpleTrustedSourcesWithOneCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                var trustedSource1 = new TrustedSource("nuget.org");
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));

                var trustedSource2 = new TrustedSource("temp.org");
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

                var trustedSources = new List<TrustedSource>
                {
                    trustedSource1,
                    trustedSource2
                };

                // Act
                trustedSourceProvider.SaveTrustedSources(trustedSources);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(trustedSources.Count);
                actualTrustedSources.ShouldBeEquivalentTo(trustedSources);
            }
        }

        [Fact]
        public void SavesMultpleTrustedSourcesWithMultipleCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                var trustedSource1 = new TrustedSource("nuget.org");
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512));

                var trustedSource2 = new TrustedSource("temp.org");
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA384));
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH4", "SUBJECT_NAME4", HashAlgorithmName.SHA512));

                var trustedSources = new List<TrustedSource>
                {
                    trustedSource1,
                    trustedSource2
                };

                // Act
                trustedSourceProvider.SaveTrustedSources(trustedSources);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(trustedSources.Count);
                actualTrustedSources.ShouldBeEquivalentTo(trustedSources);
            }
        }

        [Fact]
        public void SavesNewTrustedSourceIntoNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
</configuration>";

            var config2 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockBaseDirectory, nugetConfigPath), Path.Combine(mockChildDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }


        [Fact]
        public void SavesUpdatedTrustedSourceIntoNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var config2 = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var expectedValues = new List<CertificateTrustEntry>
            {
                new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256),
                new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.Should().Contain(trustedSource);
            }
        }
    }
}
