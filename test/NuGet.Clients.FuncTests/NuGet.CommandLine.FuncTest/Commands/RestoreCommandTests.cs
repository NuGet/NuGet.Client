// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests restore command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection(SignCommandTestCollection.Name)]
    public class RestoreCommandTests
    {
        private static readonly string _NU3008Message = "The package integrity check failed.";
        private static readonly string _NU3008 = $"NU3008: {_NU3008Message}";
        private static readonly string _NU3027Message = "The signature should be timestamped to enable long-term signature validity after the certificate has expired.";
        private static readonly string _NU3027 = $"NU3027: {_NU3027Message}";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly string _nugetExePath;

        public RestoreCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public async Task Restore_TamperedPackage_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard2.0"));

                var packageX = new SimpleTestPackageContext("X", "9.0.0");
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, packageX, pathContext.PackageSource);
                SignedArchiveTestUtility.TamperWithPackage(signedPackagePath);

                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var args = new string[]
                {
                    projectA.ProjectPath,
                    "-Source",
                    pathContext.PackageSource
                };

                // Act
                var result = RunRestore(_nugetExePath, pathContext, expectedExitCode: 1, additionalArgs: args);
                var reader = new LockFileFormat();
                var lockFile = reader.Read(projectA.AssetsFileOutputPath);
                var errors = lockFile.LogMessages.Where(m => m.Level == LogLevel.Error);
                var warnings = lockFile.LogMessages.Where(m => m.Level == LogLevel.Warning);
            
                // Assert
                result.ExitCode.Should().Be(1);
                result.Errors.Should().Contain(_NU3008);
                result.AllOutput.Should().Contain($"WARNING: {_NU3027}");

                errors.Count().Should().Be(1);
                errors.First().Code.Should().Be(NuGetLogCode.NU3008);
                errors.First().Message.Should().Be(_NU3008Message);
                errors.First().LibraryId.Should().Be(packageX.ToString());

                warnings.Count().Should().Be(1);
                warnings.First().Code.Should().Be(NuGetLogCode.NU3027);
                warnings.First().Message.Should().Be(_NU3027Message);
                warnings.First().LibraryId.Should().Be("X.9.0.0");
            }
        }

        [Fact]
        public void GetCertificateChain_WithUntrustedRoot_Throws()
        {
            using (var chainHolder = new X509ChainHolder())
            using (var rootCertificate = SigningTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SigningTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SigningTestUtility.GetCertificate("leaf.crt"))
            {
                var chain = chainHolder.Chain;
                var extraStore = new X509Certificate2Collection() { rootCertificate, intermediateCertificate };
                var logger = new TestLogger();

                var exception = Assert.ThrowsAsync<SignatureException>(
                    async () => CertificateChainUtility.GetCertificateChain(
                        leafCertificate,
                        extraStore,
                        logger,
                        CertificateType.Signature));

                Assert.Equal(NuGetLogCode.NU3018, exception.Result.Code);
                Assert.Equal("Certificate chain validation failed.", exception.Result.Message);

                Assert.Equal(1, logger.Errors);
                Assert.Equal(RuntimeEnvironmentHelper.IsWindows ? 2 : 1, logger.Warnings);

                SigningTestUtility.AssertUntrustedRoot(logger.LogMessages, LogLevel.Error);
                SigningTestUtility.AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    SigningTestUtility.AssertRevocationStatusUnknown(logger.LogMessages, LogLevel.Warning);
                }
            }
        }

        public static CommandRunnerResult RunRestore(string nugetExe, SimpleTestPathContext pathContext, int expectedExitCode = 0, params string[] additionalArgs)
        {
            // Store the dg file for debugging
            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_HTTP_CACHE_PATH", pathContext.HttpCacheFolder }
            };

            var args = new string[]
            {
                "restore",
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
