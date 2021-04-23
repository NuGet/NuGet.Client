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

        [Fact]
        public void DotnetTrust_Implicit_ListAction_Succeeds()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigFileName = "NuGet.Config";
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
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                File.WriteAllText(nugetConfigPath, nugetConfigContent);

                //Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    pathContext.WorkingDirectory,
                    $"nuget trust --configfile {nugetConfigPath}");

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [Fact]
        public void DotnetTrust_Implicity_ListAction_EmptySettings_Succeeds()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigFileName = "NuGet.Config";
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);

                // Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust --configfile {nugetConfigPath}");

                //// Assert
                result.Success.Should().BeTrue();
                result.Output.Should().Contain("There are no trusted signers.");
            }
        }

        [Fact]
        public void DotnetTrust_ListAction_Succeeds()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigFileName = "NuGet.Config";
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);

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
                File.WriteAllText(nugetConfigPath, nugetConfigContent);

                //Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    pathContext.WorkingDirectory,
                    $"nuget trust list --configfile {nugetConfigPath}");

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [Fact]
        public void DotnetTrust_ListAction_WithRelativePathNugetConfig_Succeeds()
        {
            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                var nugetConfigFileName = "NuGet.Config";
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
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);
                File.WriteAllText(nugetConfigPath, nugetConfigContent);

                //Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust list --configfile ..{Path.DirectorySeparatorChar}{nugetConfigFileName}");

                // Assert
                result.Success.Should().BeTrue();
                SettingsTestUtils.RemoveWhitespace(result.Output).Should().Contain(SettingsTestUtils.RemoveWhitespace(expectedAuthorContent));
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DotnetTrust_AuthorAction_RelativePathConfileFile_Succeeds(bool allowUntrustedRoot)
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
                CommandRunnerResult resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust author nuget {signedPackagePath}  {allowUntrustedRootArg} --configfile ..{Path.DirectorySeparatorChar}{nugetConfigFileName}");

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

        [CIOnlyFact]
        public async Task DotnetTrust_AuthorAction_RelativePathConfileFile_WithoutExistingTrustedSignersSection_Succeeds()
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
                    </configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFileName, pathContext.WorkingDirectory, config);
                var nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, nugetConfigFileName);

                // Act
                CommandRunnerResult resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust author nuget {signedPackagePath} --configfile ..{Path.DirectorySeparatorChar}{nugetConfigFileName}");

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
                                 <certificate fingerprint = ""{certFingerprint}"" hashAlgorithm = ""SHA256"" allowUntrustedRoot = ""false""/>
                            </author>
                      </trustedSigners>
                    </configuration>");

                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(expectedResult);
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DotnetTrust_AuthorAction_AbsolutePathConfileFile_Succeeds(bool allowUntrustedRoot)
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
                CommandRunnerResult resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust author nuget {signedPackagePath}  {allowUntrustedRootArg} --configfile {nugetConfigPath}");

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
        public async Task DotnetTrust_RepositoryAction_Succeeds(bool allowUntrustedRoot, string owners)
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
                CommandRunnerResult resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust repository nuget {signedPackagePath}  {allowUntrustedRootArg} {ownersArgs} --configfile {nugetConfigPath}");

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
        public async Task DotnetTrust_CertificateFingerPrintAction_Succeeds(bool allowUntrustedRoot)
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
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust certificate {authorName} {certFingerprint} {allowUntrustedRootArg}  --algorithm SHA256 --configfile {nugetConfigPath}");

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, _successfulAddTrustedSigner, "author", authorName));

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
        public async Task DotnetTrust_CertificateFingerPrintAction_WithExistingSigner_AppendSucceeds(bool allowUntrustedRoot)
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
                CommandRunnerResult resultAdd = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust certificate {authorName} {certFingerprint} {allowUntrustedRootArg}  --algorithm SHA256 --configfile {nugetConfigPath}");

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
        public async Task DotnetTrust_RemoveAction_Succeeds()
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
                CommandRunnerResult resultSync = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust remove {repositoryName} --configfile {nugetConfigPath}");

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
        public async Task DotnetTrust_RemoveAction_WrongName_NoChange()
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
                CommandRunnerResult resultSync = _msbuildFixture.RunDotnet(
                    pathContext.SolutionRoot,
                    $"nuget trust remove {repositoryWrongName} --configfile {nugetConfigPath}");

                // Assert
                resultSync.Success.Should().BeTrue();
                resultSync.AllOutput.Should().Contain(string.Format(CultureInfo.CurrentCulture, "No trusted signers with the name: '{0}' were found.", repositoryWrongName));
                SettingsTestUtils.RemoveWhitespace(File.ReadAllText(nugetConfigPath)).Should().Be(SettingsTestUtils.RemoveWhitespace(config));
            }
        }
    }
}
