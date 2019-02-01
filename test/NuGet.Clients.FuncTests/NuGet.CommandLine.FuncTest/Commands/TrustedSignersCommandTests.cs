// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.CommandLine.Commands;
using NuGet.CommandLine.Test;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration.Test;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using static NuGet.Commands.TrustedSignersArgs;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests trusted-signers command
    /// </summary>
    [Collection(SignCommandTestCollection.Name)]
    public class TrustedSignersCommandTests
    {
        private const string _trustedSignersHelpStringFragment = "usage: NuGet trusted-signers <List|Add|Remove|Sync> [options]";
        private const string _successfulActionTrustedSigner = "Successfully {0} the trusted {1} '{2}'.";
        private const string _successfulAddTrustedSigner = "Successfully added a trusted {0} '{1}'.";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepoTestCert;
        private readonly string _nugetExePath;

        public TrustedSignersCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = fixture.TrustedTestCertificate;
            _trustedRepoTestCert = fixture.TrustedRepoTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [Fact]
        public async Task TrustedSignersCommand_NoAction_DefaultsToListAsync()
        {
            var commandRunner = new Mock<ITrustedSignersCommandRunner>();
            var mockConsole = new Mock<IConsole>();

            var command = new TrustedSignersCommand()
            {
                TrustedSignersCommandRunner = commandRunner.Object,
                Console = mockConsole.Object
            };

            // Act
            command.Execute();
            await command.ExecuteCommandAsync();

            commandRunner.Verify(r => r.ExecuteCommandAsync(It.Is<TrustedSignersArgs>(a => a.Action == TrustedSignersAction.List)));
        }

        [Fact]
        public async Task TrustedSignersCommand_SendsArgumentsCorrectlyToCommandRunnerAsync()
        {
            var commandRunner = new Mock<ITrustedSignersCommandRunner>();
            var mockConsole = new Mock<IConsole>();

            var expectedName = "signerName";
            var expectedServiceIndex = @"https://serviceIndex.test";
            var expectedCertificateFingerprint = "abcdefg";
            var expectedFingerprintAlgorithm = "SHA256";
            var expectedAllowUntrustedRoot = true;
            var expectedAuthor = true;
            var expectedRepository = true;
            var expectedAction = TrustedSignersAction.Add;
            var expectedOwners = new List<string>() { "one", "two", "three" };
            var expectedPackagePath = @"C:\\package\\path\\test";

            var command = new TrustedSignersCommand()
            {
                TrustedSignersCommandRunner = commandRunner.Object,
                Name = expectedName,
                ServiceIndex = expectedServiceIndex,
                CertificateFingerprint = expectedCertificateFingerprint,
                FingerprintAlgorithm = expectedFingerprintAlgorithm,
                AllowUntrustedRoot = expectedAllowUntrustedRoot,
                Author = expectedAuthor,
                Repository = expectedRepository,
                Owners = expectedOwners,
                Console = mockConsole.Object
            };
            command.Arguments.Add(expectedAction.ToString());
            command.Arguments.Add(expectedPackagePath);

            // Act
            command.Execute();
            await command.ExecuteCommandAsync();

            commandRunner.Verify(r =>
                r.ExecuteCommandAsync(It.Is<TrustedSignersArgs>(a =>
                    a.Action == expectedAction &&
                    string.Equals(a.Name, expectedName, StringComparison.Ordinal) &&
                    string.Equals(a.ServiceIndex, expectedServiceIndex, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.CertificateFingerprint, expectedCertificateFingerprint, StringComparison.Ordinal) &&
                    string.Equals(a.FingerprintAlgorithm, expectedFingerprintAlgorithm, StringComparison.Ordinal) &&
                    a.AllowUntrustedRoot == expectedAllowUntrustedRoot &&
                    a.Author == expectedAuthor &&
                    a.Repository == expectedRepository &&
                    a.Owners.SequenceEqual(expectedOwners) &&
                    string.Equals(a.PackagePath, expectedPackagePath, StringComparison.Ordinal))));
        }

        [Theory]
        [InlineData("list -CertificateFingerprint one -FingerprintAlgorithm SHA256")]
        [InlineData("add -CertificateFingerprint one -FingerprintAlgorithm SHA256")]
        [InlineData("add -FingerprintAlgorithm SHA256")]
        [InlineData("add -Name blah -Repository")]
        [InlineData("add -Name blah extraArg")]
        [InlineData("add .\\package -Name blah")]
        [InlineData("add .\\package -Name blah -Repository -Author")]
        [InlineData("add .\\package -Name blah -CertificateFingerprint one -FingerprintAlgorithm SHA256")]
        [InlineData("add .\\package -Name blah -ServiceIndex https://v3.test")]
        [InlineData("add .\\package -Name blah -Author -Owners one;two")]
        [InlineData("remove")]
        [InlineData("remove extraArg")]
        [InlineData("remove -Name blah -Repository")]
        [InlineData("remove -Name blah -CertificateFingerprint one")]
        [InlineData("remove -Name blah -Owners one")]
        [InlineData("remove -Name blah extraArg")]
        [InlineData("sync")]
        [InlineData("sync extraArg")]
        [InlineData("sync -Name blah -Repository")]
        [InlineData("sync -Name blah -CertificateFingerprint one")]
        [InlineData("sync -Name blah -Owners one")]
        [InlineData("sync -Name blah extraArg")]
        [InlineData("notSupportedAction")]
        public void TrustedSignersCommand_Failure_InvalidArguments_HelpMessage(string args)
        {
            // Arrange & Act 
            var result = CommandRunner.Run(
                _nugetExePath,
                Directory.GetCurrentDirectory(),
                $"trusted-signers {args}",
                waitForExit: true);

            // Assert

            // TODO: Instead of these assertions there should be a call to :
            //  Util.VerifyResultFailure(result, TrustedSignersHelpStringFragment, checkErrorMsgOnStdErr: false);

            Assert.True(
                result.Item1 != 0,
                "nuget.exe DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                result.Item2.Contains(_trustedSignersHelpStringFragment),
                "Expected error is " + _trustedSignersHelpStringFragment + ". Actual error is " + result.Item3);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TrustedSignersCommand_AddTrustedSigner_WithCertificiateFingerprint_AddsItSuccesfullyToConfig(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            // Arrange
            using (var dir = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "-AllowUntrustedRoot" : string.Empty;

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers add -Name signer -CertificateFingerprint abcdefg -FingerprintAlgorithm SHA256 {allowUntrustedRootArg} -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSigners>
        <author name=""signer"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{allowUntrustedRoot.ToString().ToLower()}"" />
        </author>
    </trustedSigners>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TrustedSignersCommand_AddTrustedSigner_WithExistingSigner_UpdatesItSuccesfullyInConfig(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSigners>
        <author name=""signer"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
</configuration>";

            // Arrange
            using (var dir = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "-AllowUntrustedRoot" : string.Empty;

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers add -Name signer -CertificateFingerprint hijklmn -FingerprintAlgorithm SHA256 {allowUntrustedRootArg} -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulActionTrustedSigner, "updated", "signer", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSigners>
        <author name=""signer"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
            <certificate fingerprint=""hijklmn"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{allowUntrustedRoot.ToString().ToLower()}"" />
        </author>
    </trustedSigners>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TrustedSignersCommand_AddTrustedSigner_WithAuthorSignedPackage_AddsItSuccesfullyToConfigAsync(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            // Arrange
            var package = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            using (var cert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(cert, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(cert, package, dir);

                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "-AllowUntrustedRoot" : string.Empty;

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers add {signedPackagePath} -Name signer -Author {allowUntrustedRootArg} -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<trustedSigners>
    <author name=""signer"">
        <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{allowUntrustedRoot.ToString().ToLower()}"" />
    </author>
</trustedSigners>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true, null)]
        [InlineData(true, "one;two;three")]
        [InlineData(false, null)]
        [InlineData(false, "one;two;three")]
        public async Task TrustedSignersCommand_AddTrustedSigner_WithRepositorySignedPackage_AddsItSuccesfullyToConfigAsync(bool allowUntrustedRoot, string owners)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            // Arrange
            var package = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            using (var cert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(cert, package, dir, new Uri(repoServiceIndex));

                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "-AllowUntrustedRoot" : string.Empty;
                var ownersArgs = string.Empty;
                var expectedOwners = string.Empty;

                if (owners != null)
                {
                    ownersArgs = $"-Owners {owners}";
                    expectedOwners = $"<owners>{owners}</owners>";
                }

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers add {signedPackagePath} -Name signer -Repository {allowUntrustedRootArg} {ownersArgs} -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "repository", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<trustedSigners>
    <repository name=""signer"" serviceIndex=""{repoServiceIndex}"">
        <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{allowUntrustedRoot.ToString().ToLower()}"" />
        {expectedOwners}
    </repository>
</trustedSigners>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true, null)]
        [InlineData(true, "one;two;three")]
        [InlineData(false, null)]
        [InlineData(false, "one;two;three")]
        public async Task TrustedSignersCommand_AddTrustedSigner_WithRepositoryCountersignedPackage_AddsItSuccesfullyToConfigAsync(bool allowUntrustedRoot, string owners)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            // Arrange
            var package = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoCert = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(repoCert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var authorSignedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(authorCert, package, dir);
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoCert, authorSignedPackagePath, dir, new Uri(repoServiceIndex));

                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "-AllowUntrustedRoot" : string.Empty;
                var ownersArgs = string.Empty;
                var expectedOwners = string.Empty;

                if (owners != null)
                {
                    ownersArgs = $"-Owners {owners}";
                    expectedOwners = $"<owners>{owners}</owners>";
                }

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers add {signedPackagePath} -Name signer -Repository {allowUntrustedRootArg} {ownersArgs} -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "repository", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<trustedSigners>
    <repository name=""signer"" serviceIndex=""{repoServiceIndex}"">
        <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{allowUntrustedRoot.ToString().ToLower()}"" />
        {expectedOwners}
    </repository>
</trustedSigners>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [Fact]
        public void TrustedSignersCommand_RemoveTrustedSigner_RemovesItSuccessfullyFromConfig()
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSigners>
        <author name=""signer"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
</configuration>";

            // Arrange
            using (var dir = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, dir, config);
                var nugetConfigPath = Path.Combine(dir, nugetConfigFileName);

                // Act
                var commandResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"trusted-signers remove -Name signer -Config {nugetConfigPath}",
                    waitForExit: true);

                // Assert
                commandResult.Success.Should().BeTrue();
                commandResult.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulActionTrustedSigner, "removed", "signer", "signer"));

                var expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }
    }
}
