// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Utility;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests install command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection(SignCommandTestCollection.Name)]
    public class InstallCommandTests
    {
        private const string _NU3008 = "NU3008: The package integrity check failed.";
        private const string _NU3012 = "NU3012: The author primary signature found a chain building issue: The certificate is revoked.";
        private const string _NU3018 = "NU3018: The author primary signature found a chain building issue: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private const string _NU3027 = "NU3027: The signature should be timestamped to enable long-term signature validity after the certificate has expired.";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private string _nugetExePath;

        public InstallCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public async Task Install_AuthorSignedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var context = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, context.WorkingDirectory);

                var args = new string[]
                {
                    nupkg.Id,
                    "-Version",
                    nupkg.Version,
                    "-DirectDownload",
                    "-NoCache",
                    "-Source",
                    context.WorkingDirectory,
                    "-OutputDirectory",
                    Path.Combine(context.WorkingDirectory, "packages")
                };

                // Act
                var result = RunInstall(_nugetExePath, context, expectedExitCode: 0, additionalArgs: args);

                // Assert
                result.ExitCode.Should().Be(0);
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");
            }
        }


        [CIOnlyFact]
        public async Task Install_RepoSignedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var context = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, context.WorkingDirectory, new Uri("https://v3serviceIndex.test/api/index.json"));

                var args = new string[]
                {
                    nupkg.Id,
                    "-Version",
                    nupkg.Version,
                    "-DirectDownload",
                    "-NoCache",
                    "-Source",
                    context.WorkingDirectory,
                    "-OutputDirectory",
                    Path.Combine(context.WorkingDirectory, "packages")
                };

                // Act
                var result = RunInstall(_nugetExePath, context, expectedExitCode:0, additionalArgs: args);

                // Assert
                result.ExitCode.Should().Be(0);
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");
            }
        }

        [CIOnlyFact]
        public async Task Install_UntrustedCertSignedPackage_WarnsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var context = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_testFixture.UntrustedSelfIssuedCertificateInCertificateStore))
            {
                await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, context.WorkingDirectory);

                var args = new string[]
                {
                    nupkg.Id,
                    "-Version",
                    nupkg.Version,
                    "-DirectDownload",
                    "-NoCache",
                    "-Source",
                    context.WorkingDirectory,
                    "-OutputDirectory",
                    Path.Combine(context.WorkingDirectory, "packages")
                };

                // Act
                var result = RunInstall(_nugetExePath, context, expectedExitCode: 0, additionalArgs: args);

                // Assert
                result.ExitCode.Should().Be(0);
                result.AllOutput.Should().Contain($"WARNING: {_NU3018}");
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");
            }
        }

        [CIOnlyFact]
        public async Task Install_TamperedPackage_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var context = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, context.WorkingDirectory);
                SignedArchiveTestUtility.TamperWithPackage(signedPackagePath);

                var args = new string[]
                {
                    nupkg.Id,
                    "-Version",
                    nupkg.Version,
                    "-DirectDownload",
                    "-NoCache",
                    "-Source",
                    context.WorkingDirectory,
                    "-OutputDirectory",
                    Path.Combine(context.WorkingDirectory, "packages")
                };

                // Act
                var result = RunInstall(_nugetExePath, context, expectedExitCode: 1, additionalArgs: args);

                // Assert
                result.ExitCode.Should().Be(1);
                result.Errors.Should().Contain(_NU3008);
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");
            }
        }

        [CIOnlyFact]
        public async Task Install_TamperedAndRevokedCertificateSignaturePackage_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var issueOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
            var bcCertificate = certificateAuthority.IssueCertificate(issueOptions);

            using (var context = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(bcCertificate.GetEncoded()))
            {
                testCertificate.PrivateKey = DotNetUtilities.ToRSA(issueOptions.KeyPair.Private as RsaPrivateCrtKeyParameters);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, context.WorkingDirectory);
                SignedArchiveTestUtility.TamperWithPackage(signedPackagePath);
                certificateAuthority.Revoke(
                    bcCertificate,
                    RevocationReason.KeyCompromise,
                    DateTimeOffset.UtcNow);

                var args = new string[]
                {
                    nupkg.Id,
                    "-Version",
                    nupkg.Version,
                    "-DirectDownload",
                    "-NoCache",
                    "-Source",
                    context.WorkingDirectory,
                    "-OutputDirectory",
                    Path.Combine(context.WorkingDirectory, "packages")
                };

                // Act
                var result = RunInstall(_nugetExePath, context, expectedExitCode: 1, additionalArgs: args);

                // Assert
                result.ExitCode.Should().Be(1);
                result.Errors.Should().Contain(_NU3008);
                result.Errors.Should().Contain(_NU3012);
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");
            }
        }

        public static CommandRunnerResult RunInstall(string nugetExe, SimpleTestPathContext pathContext, int expectedExitCode = 0, params string[] additionalArgs)
        {
            // Store the dg file for debugging
            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_HTTP_CACHE_PATH", pathContext.HttpCacheFolder }
            };

            var args = new string[] {
                    "install",
                    "-Verbosity",
                    "detailed"
                };

            args = args.Concat(additionalArgs).ToArray();

            // Act
            var r = CommandRunner.Run(
                nugetExe,
                pathContext.WorkingDirectory,
                string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(expectedExitCode == r.ExitCode, r.AllOutput);

            return r;
        }
    }
}
