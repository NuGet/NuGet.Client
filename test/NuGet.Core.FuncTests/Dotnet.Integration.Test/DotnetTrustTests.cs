// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration.Test;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetTrustTests
    {
        private const string _successfulAddTrustedSigner = "Successfully added a trusted {0} '{1}'.";
        private const string _successfulRemoveTrustedSigner = "Successfully removed the trusted signer '{0}'.";
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetTrustTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [CIOnlyFact]
        public void Trust_No_ActionCommand_DefaultTo_ListAction_Success()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                        <trustedSigners>
                            <author name=""signer"">
                                <certificate fingerprint=""abcdef"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
                            </author>
                        </trustedSigners>
                    </configuration>";

                var expectedAuthorContent = $@"Registered trusted signers:
                     1.   signer [author]
                          Certificate fingerprint(s):
                            SHA256 - abcdef
                    ";
                File.WriteAllText(Path.Combine(pathContext.WorkingDirectory, "NuGet.Config"), nugetConfigContent);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.WorkingDirectory,
                    $"nuget trust",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [CIOnlyFact]
        public void Trust_List_Emtpy_Success()
        {
            using (TestDirectory packageDir = TestDirectory.Create())
            {
                // Act
                var result = _msbuildFixture.RunDotnet(
                    packageDir,
                    $"nuget trust",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                result.Output.Should().Contain("There are no trusted signers.");
            }
        }

        [Fact]
        public void Trust_List_NotEmpty_Success()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                        <trustedSigners>
                            <author name=""signer"">
                                <certificate fingerprint=""abcdef"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
                            </author>
                        </trustedSigners>
                    </configuration>";

                var expectedAuthorContent = $@"Registered trusted signers:
                     1.   signer [author]
                          Certificate fingerprint(s):
                            SHA256 - abcdef
                    ";
                File.WriteAllText(Path.Combine(pathContext.WorkingDirectory, "NuGet.Config"), nugetConfigContent);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.WorkingDirectory,
                    $"nuget trust list",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [CIOnlyFact]
        public void Trust_List_NotEmpty_WithNugetConfig_Success()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                        <trustedSigners>
                            <author name=""signer"">
                                <certificate fingerprint=""abcdef"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
                            </author>
                        </trustedSigners>
                    </configuration>";

                var expectedAuthorContent = $@"Registered trusted signers:
                     1.   signer [author]
                          Certificate fingerprint(s):
                            SHA256 - abcdef
                    ";
                File.WriteAllText(Path.Combine(pathContext.WorkingDirectory, "nuget.config"), nugetConfigContent);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust list --configfile ..{Path.DirectorySeparatorChar}nuget.config",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Trust_Author_RelativePathConfileFile_Success(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                string certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource);
                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                      <packageSources>
                        <!--To inherit the global NuGet package sources remove the <clear/> line below -->
                        <clear />
                        <add key=""NuGetSource"" value=""{pathContext.PackageSource}"" />
                       </packageSources>
                      <config>
                        <add key=""signaturevalidationmode"" value=""accept"" />
                      </config>
                      <trustedSigners>
                      </trustedSigners>
                    </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "--allow-untrusted-root" : string.Empty;
                var allowUntruestedRootValue = allowUntrustedRoot ? "true" : "false";

                // Act
                var resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust author nuget {signedPackagePath}  {allowUntrustedRootArg} --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultAdd.Success.Should().BeTrue();
                resultAdd.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", "nuget"));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                      <packageSources>
                        <!--To inherit the global NuGet package sources remove the < clear /> line below-->
                        <clear/>
                        <add key = ""NuGetSource"" value = ""{pathContext.PackageSource}""/>
                       </packageSources >
                      <config>
                        <add key = ""signaturevalidationmode"" value = ""accept""/>
                      </config>
                      < trustedSigners>
                            <author name = ""nuget"">
                                 <certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""{allowUntruestedRootValue}""/>
                            </author>
                      </trustedSigners>
                    </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }

        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Trust_Author_AbsoluteConfileFile_Success(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                string certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource);
                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "--allow-untrusted-root" : string.Empty;
                var allowUntruestedRootValue = allowUntrustedRoot ? "true" : "false";

                // Act
                var resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust author nuget {signedPackagePath}  {allowUntrustedRootArg} --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultAdd.Success.Should().BeTrue();
                resultAdd.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", "nuget"));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                     <configuration>
                      < trustedSigners>
                            <author name = ""nuget"">
                                 <certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""{allowUntruestedRootValue}""/>
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
        public async Task Trust_Repository_Success(bool allowUntrustedRoot, string owners)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex));

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "--allow-untrusted-root" : string.Empty;
                var allowUntruestedRootValue = allowUntrustedRoot ? "true" : "false";
                var ownersArgs = string.Empty;
                var expectedOwners = string.Empty;

                if (owners != null)
                {
                    ownersArgs = $"--owners {owners}";
                    expectedOwners = $"<owners>{owners}</owners>";
                }

                // Act
                var resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust repository nuget {signedPackagePath}  {allowUntrustedRootArg} {ownersArgs} --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultAdd.Success.Should().BeTrue();
                resultAdd.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "repository", "nuget"));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    < trustedSigners>
                        <repository name = ""nuget"" serviceIndex=""https://serviceindex.test/v3/index.json"">
                                < certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""{allowUntruestedRootValue}""/>{expectedOwners}
                        </repository>
                    </trustedSigners>
                </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Trust_CertificateFingerPrint_Success(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex));

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "--allow-untrusted-root" : string.Empty;
                var allowUntruestedRootValue = allowUntrustedRoot ? "true" : "false";
                var authorName = "MyCompanyCert";

                // Act
                var resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust certificate {authorName} {certFingerprint} {allowUntrustedRootArg}  --algorithm SHA256 --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultAdd.Success.Should().BeTrue();
                resultAdd.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", authorName));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    < trustedSigners>
                        <author name = ""{authorName}"">
                                < certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""{allowUntruestedRootValue}""/>
                        </author>
                    </trustedSigners>
                </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Trust_CertificateFingerPrint_WithExistingSigner_UpdatesItSuccess(bool allowUntrustedRoot)
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex));

                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <trustedSigners>
        <author name=""MyCompanyCert"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
</configuration>";

                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                var allowUntrustedRootArg = allowUntrustedRoot ? "--allow-untrusted-root" : string.Empty;
                var allowUntruestedRootValue = allowUntrustedRoot ? "true" : "false";
                var authorName = "MyCompanyCert";

                // Act
                var resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust certificate {authorName} {certFingerprint} {allowUntrustedRootArg}  --algorithm SHA256 --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultAdd.Success.Should().BeTrue();
                resultAdd.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, "Successfully updated the trusted signer '{0}'.", authorName));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    < trustedSigners>
                        <author name = ""{authorName}"">
                                < certificate fingerprint = ""abcdefg"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""false""/>
                                < certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""{allowUntruestedRootValue}""/>
                        </author>
                    </trustedSigners>
                </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyFact]
        public async Task Trust_Remove_Success()
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex));
                var repositoryName = "nuget";

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <trustedSigners>
                        <repository name = ""{repositoryName}"" serviceIndex=""https://serviceindex.test/v3/index.json"">
                            <certificate fingerprint=""abcdef"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false""/>
                        </repository>
                    </trustedSigners>
                </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);

                // Act
                var resultSync = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust remove {repositoryName} --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultSync.Success.Should().BeTrue();
                resultSync.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulRemoveTrustedSigner, repositoryName));

                string expectedResult = SettingsTestUtils.RemoveWhitespace($@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyFact]
        public async Task Trust_Remove_WrongName_NoChange()
        {
            // Arrange
            var nugetConfigFileName = "NuGet.Config";
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex));
                var repositoryName = "nuget";
                var repositoryWrongName = "nuget11";

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <trustedSigners>
                        <repository name = ""{repositoryName}"" serviceIndex=""https://serviceindex.test/v3/index.json"">
                            <certificate fingerprint=""abcdef"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false""/>
                        </repository>
                    </trustedSigners>
                </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);

                // Act
                var resultSync = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust remove {repositoryWrongName} --configfile {nugetConfigPath}",
                    ignoreExitCode: true);

                // Assert
                resultSync.Success.Should().BeTrue();
                resultSync.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, "No trusted signers with the name: '{0}' were found.", repositoryWrongName));
                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(SettingsTestUtils.RemoveWhitespace(config));
            }
        }
    }
}
