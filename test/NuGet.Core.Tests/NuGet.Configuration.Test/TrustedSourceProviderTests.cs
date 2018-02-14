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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
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

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
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

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
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
                var trustedSources = trustedSourceProvider.LoadTrustedSources();

                // Assert
                trustedSources.Should().NotBeNull();
                trustedSources.Count().Should().Be(1);
                trustedSources.First().SourceName.Should().Be("nuget.org");
                trustedSources.First().ServiceIndex.Should().BeNull();
                trustedSources.First().Certificates.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void LoadsTrustedSourceWithMultipleCertificatesForPackageSourceIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
            trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256, priority: 0));
            trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH512", "SUBJECT_NAME", HashAlgorithmName.SHA512, priority: 0));
            var trustedSource2 = new TrustedSource("test.org");
            trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA512, priority: 0));

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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
                trustedSource.ServiceIndex = new ServiceIndexTrustEntry(@"https://nuget.org");
                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""https://nuget.org"" />
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
        public void WritesUpdatedTrustedSourceWithReplacedServiceIndex()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX"" />
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
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA384));
                trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX2");

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX2"" />
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA384"" />
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
        public void WritesUpdatedTrustedSourceWithAddedServiceIndex()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
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
                trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX");

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX"" />
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
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
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX");

            var expectedTrustedSources = new List<TrustedSource>()
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);


                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesNewTrustedSourceWithMultipleCertificatesAndServiceIndex()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX");

            var expectedTrustedSources = new List<TrustedSource>()
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceWithReplacedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));

            var expectedTrustedSources = new List<TrustedSource>()
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceWithAddedCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));

            var expectedTrustedSources = new List<TrustedSource>()
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesMultpleTrustedSourcesWithOneCertificate()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256, priority: 0));

                var trustedSource2 = new TrustedSource("temp.org");
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384, priority: 0));

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
        public void SavesMultpleTrustedSourcesWithMultipleCertificates()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256, priority: 0));
                trustedSource1.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512, priority: 0));
                trustedSource1.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX", priority: 0);

                var trustedSource2 = new TrustedSource("temp.org");
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA384, priority: 0));
                trustedSource2.Certificates.Add(new CertificateTrustEntry("HASH4", "SUBJECT_NAME4", HashAlgorithmName.SHA512, priority: 0));

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
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
</configuration>";

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX");

            var expectedTrustedSources = new List<TrustedSource>
            {
                trustedSource
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
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesNewTrustedSourceIntoConfigFiles()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""test.org"" value=""Packages"" />
  </packageSources>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA384));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX");

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""test.org"" value=""Packages"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX"" />
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA384"" />
      <add key=""HASH3"" value=""SUBJECT_NAME3"" fingerprintAlgorithm=""SHA512"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockChildDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }


        [Fact]
        public void SavesUpdatedTrustedSourceIntoNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""test.org"" value=""Packages"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX"" />
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
    </nuget.org>
  </trustedSources>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
    </nuget.org>
  </trustedSources>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX2");

            var expectedTrustedSources = new List<TrustedSource>
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);


                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceIntoConfigFiles()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX"" />
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
    </nuget.org>
  </trustedSources>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512));
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH3", "SUBJECT_NAME3", HashAlgorithmName.SHA512));
            trustedSource.ServiceIndex = new ServiceIndexTrustEntry("SERVICE_INDEX2");

            var expectedTrustedSources = new List<TrustedSource>
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);


                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""test.org"" value=""Packages"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH3"" value=""SUBJECT_NAME3"" fingerprintAlgorithm=""SHA512"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <trustedSources>
    <nuget.org>
      <add key=""serviceIndex"" value=""SERVICE_INDEX2"" />
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
    </nuget.org>
  </trustedSources>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockChildDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavesUpdatedTrustedSourceIntoNestedSettingsRemovingOldEntries()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
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

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            var trustedSource = new TrustedSource("nuget.org");
            trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));

            var expectedTrustedSources = new List<TrustedSource>
            {
                trustedSource
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.SaveTrustedSource(trustedSource);

                // Assert
                AssertTrustedSources(trustedSourceProvider.LoadTrustedSources(), expectedTrustedSources);
            }
        }

        [Fact]
        public void DeletesTrustedSource()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
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

                // Act
                trustedSourceProvider.DeleteTrustedSource("nuget.org");

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Should().NotBeNull();
                actualTrustedSources.Count().Should().Be(0);
            }
        }

        [Fact]
        public void DeletesTrustedSourceInConfigFile()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
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

                // Act
                trustedSourceProvider.DeleteTrustedSource("nuget.org");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
  </packageSources>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void DeletesTrustedSourceButLeavesOthers()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key='nuget.org' value='https://nuget.org' />
    <add key='temp.org' value='https://temp.org' />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
    </nuget.org>
    <temp.org>
      <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA256' />
    </temp.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256, priority: 0));

                // Act
                trustedSourceProvider.DeleteTrustedSource("temp.org");

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Should().NotBeNull();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.First().ShouldBeEquivalentTo(trustedSource);
            }
        }

        [Fact]
        public void DeletesTrustedSourceButLeavesOthersInConfigFile()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""temp.org"" value=""https://temp.org"" />
  </packageSources>
  <trustedSources>
    <nuget.org>
      <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
    </nuget.org>
    <temp.org>
      <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA256"" />
    </temp.org>
  </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256));

                // Act
                trustedSourceProvider.DeleteTrustedSource("temp.org");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://nuget.org"" />
    <add key=""temp.org"" value=""https://temp.org"" />
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
        public void DeletesTrustedSourceFromNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
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

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
    </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.DeleteTrustedSource("nuget.org");

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Should().NotBeNull();
                actualTrustedSources.Count().Should().Be(0);
            }
        }

        [Fact]
        public void DeletesTrustedSourceFromNestedSettingsInConfigFiles()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
        </nuget.org>
    </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.DeleteTrustedSource("nuget.org");

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockChildDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void DeletesTrustedSourceFromNestedSettingsButLeavesOthers()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='https://nuget.org' />
        <add key='test.org' value='Packages' />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key='HASH' value='SUBJECT_NAME' fingerprintAlgorithm='SHA256' />
        </nuget.org>
        <temp.org>
            <add key='HASH3' value='SUBJECT_NAME3' fingerprintAlgorithm='SHA256' />
        </temp.org>
    </trustedSources>
</configuration>";

            var config2 = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key='HASH2' value='SUBJECT_NAME2' fingerprintAlgorithm='SHA512' />
        </nuget.org>
        <temp.org>
            <add key='HASH4' value='SUBJECT_NAME4' fingerprintAlgorithm='SHA256' />
        </temp.org>
    </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);
                var trustedSource = new TrustedSource("nuget.org");
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH", "SUBJECT_NAME", HashAlgorithmName.SHA256, 1));
                trustedSource.Certificates.Add(new CertificateTrustEntry("HASH2", "SUBJECT_NAME2", HashAlgorithmName.SHA512, 2));

                // Act
                trustedSourceProvider.DeleteTrustedSource("temp.org");

                // Assert
                var actualTrustedSources = trustedSourceProvider.LoadTrustedSources();
                actualTrustedSources.Should().NotBeNull();
                actualTrustedSources.Count().Should().Be(1);
                actualTrustedSources.First().ShouldBeEquivalentTo(trustedSource);
            }
        }

        [Fact]
        public void DeletesTrustedSourceFromNestedSettingsButLeavesOthersInConfigFile()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
    <trustedSources>
        <nuget.org>
            <add key=""HASH"" value=""SUBJECT_NAME"" fingerprintAlgorithm=""SHA256"" />
        </nuget.org>
        <temp.org>
            <add key=""HASH3"" value=""SUBJECT_NAME3"" fingerprintAlgorithm=""SHA256"" />
        </temp.org>
    </trustedSources>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
        </nuget.org>
        <temp.org>
            <add key=""HASH4"" value=""SUBJECT_NAME4"" fingerprintAlgorithm=""SHA256"" />
        </temp.org>
    </trustedSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);
                var trustedSourceProvider = new TrustedSourceProvider(settings);

                // Act
                trustedSourceProvider.DeleteTrustedSource("temp.org");

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSources>
        <nuget.org>
            <add key=""HASH2"" value=""SUBJECT_NAME2"" fingerprintAlgorithm=""SHA512"" />
        </nuget.org>
    </trustedSources>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockChildDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        private void AssertTrustedSources(IEnumerable<TrustedSource> actual, IEnumerable<TrustedSource> expected)
        {
            actual.Should().NotBeNull();
            expected.Should().NotBeNull();
            actual.Count().Should().Be(expected.Count());

            var expectedLookUp = expected.ToLookup(s => s.SourceName, StringComparer.OrdinalIgnoreCase);

            foreach (var source in actual)
            {
                var matchingValues = expectedLookUp[source.SourceName];
                matchingValues.Count().Should().Be(1);

                var matchingValue = matchingValues.FirstOrDefault();
                matchingValue.Should().NotBeNull();

                if (matchingValue.ServiceIndex != null)
                {
                    source.ServiceIndex.Should().NotBeNull();
                    string.Equals(source.ServiceIndex.Value, matchingValue.ServiceIndex.Value, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
                }

                foreach (var cert in source.Certificates)
                {
                    matchingValue.Certificates.Should().Contain(cert);
                }
            }
        }
    }
}
