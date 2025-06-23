// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio.Options;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test.Options
{
    public class PackageSourceValidatorTests
    {
        [Theory]
        [InlineData("TestSource", "TestSource")]
        [InlineData(" TestSource ", " TestSource ")]
        [InlineData("TestSource ", "TestSource ")]
        [InlineData(" TestSource", "TestSource")]
        [InlineData("TestSource", " TestSource")]
        [InlineData("TestSource ", "TestSource")]
        [InlineData("TestSource", "TestSource ")]
        public void ValidateUniquenessOrThrow_DuplicateSourceNames_ThrowsArgumentException(string name1, string name2)
        {
            // Arrange
            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: "https://testsource1.com", name1),
                new PackageSource(source: "https://testsource2.com", name2)
            };

            // Act
            Action act = () => PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Be(Strings.Error_PackageSource_UniqueName);
        }

        [Fact]
        public void ValidateUniquenessOrThrow_ExactDuplicate_RemoteSources_ThrowsArgumentException()
        {
            // Arrange
            string duplicateSource = "https://testsource.com";

            var packageSources = new List<PackageSource>
            {
                new PackageSource(duplicateSource, name: "TestSource1"),
                new PackageSource(duplicateSource, name: "TestSource2")
            };

            // Act
            Action act = () => PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Be(Strings.Error_PackageSource_UniqueSource);
        }

        [Fact]
        public void ValidateUniquenessOrThrow_DuplicateWhenIgnoringTrailingSlash_RemoteSources_Succeeds()
        {
            // Arrange
            string source1 = "https://testsource.com";
            string source2 = $"{source1}/";

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source1, name: "TestSource1"),
                new PackageSource(source2, name: "TestSource2")
            };

            // Act
            PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            // No exception should be thrown, indicating success.
        }

        [Theory]
        [InlineData(@" custom://testsource.com/", @"custom://testsource.com/")]
        [InlineData(@"custom://testsource.com/", @" custom://testsource.com/")]
        [InlineData(@"custom://testsource.com/ ", @"custom://testsource.com/")]
        [InlineData(@"custom://testsource.com/", @" custom://testsource.com/ ")]
        [InlineData(@" https://testsource.com", @"https://testsource.com")]
        [InlineData(@"https://testsource.com", @" https://testsource.com")]
        [InlineData(@"https://testsource.com ", @"https://testsource.com")]
        [InlineData(@"https://testsource.com", @"https://testsource.com ")]
        [InlineData(@" https://api.nuget.org/v3/index.json", @"https://api.nuget.org/v3/index.json")]
        [InlineData(@"https://api.nuget.org/v3/index.json", @" https://api.nuget.org/v3/index.json")]
        [InlineData(@"https://api.nuget.org/v3/index.json ", @"https://api.nuget.org/v3/index.json")]
        [InlineData(@"https://api.nuget.org/v3/index.json", @"https://api.nuget.org/v3/index.json ")]
        public void ValidateUniquenessOrThrow_DuplicateWhenIgnoringWhitespace_RemoteSources_ThrowsArgumentException(string source1, string source2)
        {
            // Arrange
            var packageSources = new List<PackageSource>
            {
                new PackageSource(source1, name: "TestSource1"),
                new PackageSource(source2, name: "TestSource2")
            };

            // Act
            Action act = () => PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Be(Strings.Error_PackageSource_UniqueSource);
        }

        [Theory]
        [InlineData(@"\\server\share", @"\\server\share\")]
        [InlineData(@"C:\path", @"C:\path\")]
        [InlineData(@"C:\path\to", @"C:\path\to\")]
        public void ValidateUniquenessOrThrow_DuplicateWhenIgnoringTrailingSlash_PathSources_ThrowsArgumentException(string source1, string source2)
        {
            // Arrange
            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: source1, name: "TestSource1"),
                new PackageSource(source: source2, name: "TestSource2")
            };

            // Act
            Action act = () => PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Be(Strings.Error_PackageSource_UniqueSource);
        }

        [Theory]
        [InlineData(@"http://")]
        [InlineData(@"https://")]
        [InlineData(@"https:// ")]
        public void EnsureValidSources_MissingProtocol_RemoteSource_ThrowsArgumentOutOfRangeException(string invalidSource)
        {
            // Arrange
            var packageSource = new PackageSource(source: invalidSource, name: "TestSource1");

            // Act
            Action act = () => PackageSourceValidator.EnsureValidSources(packageSource);

            // Assert
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(act);
            exception.ParamName.Should().Be(nameof(PackageSource.Source));
            exception.Message.Should().StartWith(Strings.Error_PackageSource_InvalidSource);
        }

        [Theory]
        [InlineData(@" https://")]
        [InlineData(@"ftp://")]
        [InlineData(@"http:/")]
        public void EnsureValidSources_InvalidSource_RemoteSource_ThrowsArgumentOutOfRangeException(string invalidSource)
        {
            // Arrange
            var packageSource = new PackageSource(source: invalidSource, name: "TestInvalidSource");

            // Act
            Action act = () => PackageSourceValidator.EnsureValidSources(packageSource);

            // Assert
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(act);
            exception.ParamName.Should().Be(nameof(PackageSource.Source));
            exception.Message.Should().StartWith(Strings.Error_PackageSource_InvalidSource);
        }

        [Theory]
        [InlineData(@"custom://testsource.com/")]
        [InlineData(@"https://testsource.com/")]
        [InlineData(@"https://api.nuget.org/v3/index.json")]
        [InlineData(@"https://1")]
        [InlineData(@"https://testsource.com")]
        [InlineData(@"ftp://1")]
        [InlineData(@"ftp://testsource.com")]
        public void ValidateUniquenessOrThrow_ValidRemoteSources_Successful(string validSource)
        {
            // Arrange
            var packageSources = new List<PackageSource>
            {
                new PackageSource(validSource, name: "TestSource1"),
            };

            // Act
            PackageSourceValidator.ValidateUniquenessOrThrow(packageSources);

            // Assert
            // No exception should be thrown, indicating success.
        }

        [Theory]
        [InlineData(@"C")]
        [InlineData(@"http")] // Missing :// causes this to be treated as a file path.
        [InlineData(@"http:")]
        [InlineData(@"ftp")] // Missing :// causes this to be treated as a file path.
        [InlineData(@"C:")]
        [InlineData(@"C:\invalid\*\'\chars")]
        [InlineData(@"\\server\invalid\*\")]
        [InlineData(@"..\packages")]
        [InlineData(@"./configs/source.config")]
        [InlineData(@"../local-packages/")]
        public void EnsureValidSources_InvalidUncPath_ThrowsArgumentOutOfRangeException(string invalidSource)
        {
            // Arrange
            var packageSource = new PackageSource(source: invalidSource, name: "TestInvalidSource");

            // Act
            Action act = () => PackageSourceValidator.EnsureValidSources(packageSource);

            // Assert
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(act);
            exception.ParamName.Should().Be(nameof(PackageSource.Source));
            exception.Message.Should().StartWith(Strings.Error_PackageSource_InvalidSource);
        }

        [Theory]
        [InlineData(@"C:\")]
        [InlineData(@"C:\path")]
        [InlineData(@"C:\path\")]
        [InlineData(@"C:\path\to")]
        [InlineData(@"C:\path\to\")]
        public void EnsureValidSources_ValidUncPath_Successful(string validSource)
        {
            // Arrange
            var packageSource = new PackageSource(source: validSource, name: "TestValidSource");

            // Act
            PackageSourceValidator.EnsureValidSources(packageSource);

            // Assert
            // No exception should be thrown, indicating success.
        }

        [Fact]
        public void FindExistingOrCreate_NewSource_CreatesNewSource()
        {
            // Arrange
            string name = "TestSource3";
            string lookupName = name;
            string source = "https://testsource3.com";
            bool isEnabled = true;

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: "https://testsource1.com", name: "TestSource1", isEnabled: true),
                new PackageSource(source: "https://testsource2.com", name: "TestSource2", isEnabled: true)
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name);
            result.Source.Should().Be(source);
            result.IsEnabled.Should().Be(isEnabled);
        }

        [Fact]
        public void FindExistingOrCreate_NewHttpSource_CreatesNewSource_WithAllowInsecureConnectionsSetToTrue()
        {
            // Arrange
            string name = "TestSource3";
            string lookupName = name;
            string source = "http://testsource3.com";
            bool isEnabled = true;

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: "https://testsource1.com", name: "TestSource1", isEnabled: true),
                new PackageSource(source: "https://testsource2.com", name: "TestSource2", isEnabled: true)
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name);
            result.Source.Should().Be(source);
            result.IsEnabled.Should().Be(isEnabled);
            result.AllowInsecureConnections
                .Should()
                .BeTrue(because: "New HTTP sources should allow insecure connections by default.");
        }

        [Fact]
        public void FindExistingOrCreate_FoundExistingById_UpdatesName()
        {
            // Arrange
            string originalName = "TestSource1";
            string lookupName = originalName;
            string originalSource = "http://testsource1.com";

            string name = "TestSource2";
            string source = originalSource;
            bool isEnabled = true;

            bool originalAllowInsecureConnections = true;
            bool originalDisableTLSCertificateValidation = true;
            PackageSourceCredential originalCredential = GetTestPackageSourceCredential(name);

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: originalSource, name: originalName, isEnabled)
                {
                    AllowInsecureConnections = originalAllowInsecureConnections,
                    DisableTLSCertificateValidation = originalDisableTLSCertificateValidation,
                    Credentials = originalCredential
                }
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name, because: "A rename was expected.");

            // Verify unchanged properties.
            result.Source.Should().Be(source, because: "Only the name should have changed.");
            result.AllowInsecureConnections.Should().Be(originalAllowInsecureConnections, because: "Only the name should have changed.");
            result.DisableTLSCertificateValidation.Should().Be(originalDisableTLSCertificateValidation, because: "Only the name should have changed.");
            result.Credentials.Should().BeEquivalentTo(originalCredential, because: "Only the name should have changed.");
            result.IsEnabled.Should().Be(isEnabled, because: "Only the name should have changed.");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void FindExistingOrCreate_NullOrEmptyId_ThrowsArgumentException(string invalidId)
        {
            // Arrange
            string name = "TestSource1";
            string source = "http://testsource1.com";
            bool isEnabled = true;

            List<PackageSource> packageSources = new List<PackageSource>();

            // Act
            Action act = () => PackageSourceValidator.FindExistingOrCreate(invalidId, source, name, isEnabled, packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Contain(Strings.Argument_Cannot_Be_Null_Or_Empty);
            exception.ParamName.Should().Be("lookupName");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void FindExistingOrCreate_NullOrEmptyName_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            string lookupName = "TestSource1";
            string source = "http://testsource1.com";
            bool isEnabled = true;

            List<PackageSource> packageSources = new List<PackageSource>();

            // Act
            Action act = () => PackageSourceValidator.FindExistingOrCreate(lookupName, source, invalidName, isEnabled, packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Contain(Strings.Argument_Cannot_Be_Null_Or_Empty);
            exception.ParamName.Should().Be("name");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void FindExistingOrCreate_NullOrEmptySource_ThrowsArgumentException(string invalidSource)
        {
            // Arrange
            string name = "TestSource1";
            string lookupName = name;
            bool isEnabled = true;

            List<PackageSource> packageSources = new List<PackageSource>();

            // Act
            Action act = () => PackageSourceValidator.FindExistingOrCreate(lookupName, invalidSource, name, isEnabled, packageSources);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(act);
            exception.Message.Should().Contain(Strings.Argument_Cannot_Be_Null_Or_Empty);
            exception.ParamName.Should().Be("source");
        }

        [Fact]
        public void FindExistingOrCreate_FoundExistingById_UpdatesSource()
        {
            // Arrange
            string originalName = "TestSource1";
            string lookupName = originalName;
            string originalSource = "http://testsource1.com";
            bool originalAllowInsecureConnections = true;
            bool originalDisableTLSCertificateValidation = true;

            string name = originalName;
            string source = "http://testsource2.com";
            bool isEnabled = true;

            PackageSourceCredential originalCredential = GetTestPackageSourceCredential(name);

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: originalSource, name: originalName, isEnabled)
                {
                    AllowInsecureConnections = originalAllowInsecureConnections,
                    DisableTLSCertificateValidation = originalDisableTLSCertificateValidation,
                    Credentials = originalCredential
                }
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.Source.Should().Be(source, because: "A source change was expected.");

            // Verify unchanged properties.
            result.Name.Should().Be(originalName, because: "Only the source should have changed.");
            result.AllowInsecureConnections.Should().Be(originalAllowInsecureConnections, because: "Only the source should have changed.");
            result.DisableTLSCertificateValidation.Should().Be(originalDisableTLSCertificateValidation, because: "Only the source should have changed.");
            result.Credentials.Should().BeEquivalentTo(originalCredential, because: "Only the source should have changed.");
            result.IsEnabled.Should().Be(isEnabled, because: "Only the source should have changed.");
        }

        [Fact]
        public void FindExistingOrCreate_UpdateHttptoHttpsSource_RemovesAllowInsecureConnectionsFromTargetOnly()
        {
            // Arrange
            string sourceName1 = "unitTestingSourceName1";
            string sourceUrl1 = "https://testsource1.com";
            // AllowInsecureConnections is not needed but was already configured.
            bool source1AllowInsecureConnections = true;

            string sourceName2 = "unitTestingSourceName2";
            string sourceUrl2 = "http://testsource2.com";

            // AllowInsecureConnections is needed but was missing.
            bool source2AllowInsecureConnections = false;

            string sourceName3 = "unitTestingSourceName3";
            string sourceUrl3 = "http://testsource3.com";
            bool source3AllowInsecureConnections = true;

            string targetUrl = "https://testsource3.com";
            bool expectedAllowInsecureConnections = false;

            // Configure 3 existing package sources
            List<PackageSource> packageSources =
            [
                new PackageSource(sourceUrl1, sourceName1, isEnabled: true)
                {
                    AllowInsecureConnections = source1AllowInsecureConnections
                },
                new PackageSource(sourceUrl2, sourceName2, isEnabled: true)
                {
                    AllowInsecureConnections = source2AllowInsecureConnections
                },
                new PackageSource(sourceUrl3, sourceName3, isEnabled: true)
                {
                    AllowInsecureConnections = source3AllowInsecureConnections
                }
            ];

            // Act
            PackageSource result1 = PackageSourceValidator.FindExistingOrCreate(
                lookupName: sourceName1,
                sourceUrl1,
                sourceName1,
                isEnabled: true,
                packageSources);

            PackageSource result2 = PackageSourceValidator.FindExistingOrCreate(
                lookupName: sourceName2,
                sourceUrl2,
                sourceName2,
                isEnabled: true,
                packageSources);

            PackageSource result3 = PackageSourceValidator.FindExistingOrCreate(
                lookupName: sourceName3,
                targetUrl,
                sourceName3,
                isEnabled: true,
                packageSources);

            // Assert
            result1.Should().NotBeNull();
            result1.Source.Should().Be(sourceUrl1, because: "No changes were made to the package source.");
            result1.Name.Should().Be(sourceName1, because: "No changes were made to the package source.");
            result1.AllowInsecureConnections.Should().Be(source1AllowInsecureConnections, because: "No changes were made to the package source.");
            result1.IsEnabled.Should().Be(true, because: "No changes were made to the package source.");

            result2.Should().NotBeNull();
            result2.Source.Should().Be(sourceUrl2, because: "No changes were made to the package source.");
            result2.Name.Should().Be(sourceName2, because: "No changes were made to the package source.");
            result2.AllowInsecureConnections.Should().Be(source2AllowInsecureConnections, because: "No changes were made to the package source.");
            result2.IsEnabled.Should().Be(true, because: "No changes were made to the package source.");

            result3.Should().NotBeNull();
            result3.Source.Should().Be(targetUrl, because: "A source change was expected.");
            result3.Name.Should().Be(sourceName3, because: "Only the source should have changed.");
            result3.AllowInsecureConnections.Should().Be(expectedAllowInsecureConnections, because: "Changing from HTTP to HTTPS source no longer needs AllowInsecureConnections.");
            result3.IsEnabled.Should().Be(true, because: "Only the source should have changed.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindExistingOrCreate_SwitchHTTPandHTTPS_UpdatesAllowInsecureConnections(bool isOriginallyHttps)
        {
            // Arrange
            string originalSourceProtocol = isOriginallyHttps ? "https://" : "http://";
            string originalName = "TestSource1";
            string originalSource = $"{originalSourceProtocol}testsource1.com";
            bool originalAllowInsecureConnections = !isOriginallyHttps;
            bool originalDisableTLSCertificateValidation = true;

            string name = originalName;
            string lookupName = name;
            string expectedSourceProtocol = !isOriginallyHttps ? "https://" : "http://";
            bool expectedAllowInsecureConnections = isOriginallyHttps;
            string source = $"{expectedSourceProtocol}testsource1.com";
            bool isEnabled = true;

            PackageSourceCredential originalCredential = GetTestPackageSourceCredential(name);

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source: originalSource, name: originalName, isEnabled)
                {
                    AllowInsecureConnections = originalAllowInsecureConnections,
                    DisableTLSCertificateValidation = originalDisableTLSCertificateValidation,
                    Credentials = originalCredential
                }
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.Source.Should().Be(source, because: "A source change was expected.");

            // Verify unchanged properties.
            result.Name.Should().Be(originalName, because: "Only the source should have changed.");
            result.AllowInsecureConnections.Should().Be(expectedAllowInsecureConnections, because: "Updating the source to HTTP or HTTPS should add or remove AllowInsecureConnections.");
            result.DisableTLSCertificateValidation.Should().Be(originalDisableTLSCertificateValidation, because: "Only the source should have changed.");
            result.Credentials.Should().BeEquivalentTo(originalCredential, because: "Only the source should have changed.");
            result.IsEnabled.Should().Be(isEnabled, because: "Only the source should have changed.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindExistingOrCreate_FoundExistingByNameAndSource_UpdatesIsEnabled(bool originalIsEnabled)
        {
            // Arrange
            string name = "TestSource1";
            string lookupName = name;
            string source = "http://testsource1.com";
            bool isEnabled = !originalIsEnabled; // Toggle the enabled state

            bool originalAllowInsecureConnections = true;
            bool originalDisableTLSCertificateValidation = true;
            PackageSourceCredential originalCredential = GetTestPackageSourceCredential(name);

            var packageSources = new List<PackageSource>
            {
                new PackageSource(source, name, originalIsEnabled)
                {
                    AllowInsecureConnections = originalAllowInsecureConnections,
                    DisableTLSCertificateValidation = originalDisableTLSCertificateValidation,
                    Credentials = originalCredential
                }
            };

            // Act
            PackageSource result = PackageSourceValidator.FindExistingOrCreate(lookupName, source, name, isEnabled, packageSources);

            // Assert
            result.Should().NotBeNull();
            result.IsEnabled.Should().Be(isEnabled, because: "The IsEnabled state should have been toggled.");

            // Verify unchanged properties.
            result.Name.Should().Be(name, because: "Only IsEnabled should have changed.");
            result.Source.Should().Be(source, because: "Only IsEnabled should have changed.");
            result.AllowInsecureConnections.Should().Be(originalAllowInsecureConnections, because: "Only IsEnabled should have changed.");
            result.DisableTLSCertificateValidation.Should().Be(originalDisableTLSCertificateValidation, because: "Only IsEnabled should have changed.");
            result.Credentials.Should().BeEquivalentTo(originalCredential, because: "Only IsEnabled should have changed.");
            result.IsEnabled.Should().Be(isEnabled, because: "Only IsEnabled should have changed.");
        }

        private static PackageSourceCredential GetTestPackageSourceCredential(string packageSourceName)
        {
            return new(
                source: packageSourceName,
                username: "user",
                passwordText: "pass",
                isPasswordClearText: true,
                validAuthenticationTypesText: "basic");
        }
    }
}
