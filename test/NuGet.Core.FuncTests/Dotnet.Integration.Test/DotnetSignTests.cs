// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetSignTests : IClassFixture<SignCommandTestFixture>
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;
        private SignCommandTestFixture _signFixture;

        private const string _packageAlreadySignedError = "NU3001: The package already contains a signature. Please remove the existing signature before adding a new signature.";
        private readonly string _invalidPasswordErrorCode = "Invalid password was provided for the certificate file";
        private readonly string _noCertFoundErrorCode = "No certificates were found that meet all the given criteria.";
        private readonly string _chainBuildFailureErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3002.ToString();
        private readonly string _timestampUnsupportedDigestAlgorithmCode = NuGetLogCode.NU3024.ToString();

        public DotnetSignTests(MsbuildIntegrationTestFixture buildFixture, SignCommandTestFixture signFixture)
        {
            _msbuildFixture = buildFixture;
            _signFixture = signFixture;
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithTrustedCertificate_SuccceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithTrustedCertificateWithRelativePath_SuccceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFileName = "PackageA.1.0.0.nupkg";

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign .{Path.DirectorySeparatorChar}{packageFileName} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithInvalidEku_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> invalidEkuCert = _signFixture.TrustedTestCertificateWithInvalidEku;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {invalidEkuCert.Source.Cert.Thumbprint} --certificate-store-name {invalidEkuCert.StoreName} --certificate-store-location {invalidEkuCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithExpiredCertificate_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> expiredCert = _signFixture.TrustedTestCertificateExpired;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {expiredCert.Source.Cert.Thumbprint} --certificate-store-name {expiredCert.StoreName} --certificate-store-location {expiredCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithNotYetValidCertificate_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> notYetValidCert = _signFixture.TrustedTestCertificateNotYetValid;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {notYetValidCert.Source.Cert.Thumbprint} --certificate-store-name {notYetValidCert.StoreName} --certificate-store-location {notYetValidCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithTimestamping_SucceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var timestampService = await _signFixture.GetDefaultTrustedTimestampServiceAsync();
                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation} --timestamper {timestampService.Url.OriginalString}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
        public async Task DotnetSign_SignPackageWithRevokedLeafCertChain_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> revokedCert = _signFixture.RevokedTestCertificateWithChain;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {revokedCert.Source.Cert.Thumbprint} --certificate-store-name {revokedCert.StoreName} --certificate-store-location {revokedCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithUnknownRevocationCertChain_SucceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> revocationUnknownCert = _signFixture.RevocationUnknownTestCertificateWithChain;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {revocationUnknownCert.Source.Cert.Thumbprint} --certificate-store-name {revocationUnknownCert.StoreName} --certificate-store-location {revocationUnknownCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_chainBuildFailureErrorCode);
                result.AllOutput.Should().Contain(X509ChainStatusFlags.RevocationStatusUnknown.ToString());
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithOutputDirectory_SucceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var outputDir = Path.Combine(pathContext.WorkingDirectory, "Output");
                Directory.CreateDirectory(outputDir);

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation} --output {outputDir}",
                        ignoreExitCode: true);

                var signedPackagePath = Path.Combine(outputDir, "PackageA.1.0.0.nupkg");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                File.Exists(signedPackagePath).Should().BeTrue();
            }
        }

        [Fact]
        public async Task DotnetSign_ResignPackageWithoutOverwrite_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var firstResult = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation}",
                    ignoreExitCode: true);

                var secondResult = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation}",
                    ignoreExitCode: true);

                // Assert
                firstResult.Success.Should().BeTrue(because: firstResult.AllOutput);
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeFalse();
                secondResult.AllOutput.Should().Contain(_packageAlreadySignedError);
            }
        }

        [Fact]
        public async Task DotnetSign_ResignPackageWithOverwrite_SuccessAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var firstResult = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation}",
                    ignoreExitCode: true);

                var secondResult = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation} --overwrite",
                    ignoreExitCode: true);

                // Assert
                firstResult.Success.Should().BeTrue(because: firstResult.AllOutput);
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeTrue();
                secondResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithOverwrite_SuccessAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation} --overwrite",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithPfxFile_SuccessAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;

                var pfxPath = Path.Combine(pathContext.WorkingDirectory, Guid.NewGuid().ToString());
                var password = Guid.NewGuid().ToString();
                var pfxBytes = trustedCert.Source.Cert.Export(X509ContentType.Pfx, password);
                File.WriteAllBytes(pfxPath, pfxBytes);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-path {pfxPath} --certificate-password {password}",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithPfxFileOfRelativePath_SuccessAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;

                var pfxName = Guid.NewGuid().ToString() + ".pfx";
                var pfxPath = Path.Combine(pathContext.PackageSource, pfxName);
                var password = Guid.NewGuid().ToString();
                var pfxBytes = trustedCert.Source.Cert.Export(X509ContentType.Pfx, password);
                File.WriteAllBytes(pfxPath, pfxBytes);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-path .{Path.DirectorySeparatorChar}{pfxName} --certificate-password {password}",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithPfxFileWithoutPasswordAndWithNonInteractive_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                TrustedTestCert<TestCertificate> trustedCert = _signFixture.TrustedTestCertificateChain.Leaf;
                var pfxPath = Path.Combine(pathContext.WorkingDirectory, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = trustedCert.Source.Cert.Export(X509ContentType.Pfx, password);
                File.WriteAllBytes(pfxPath, pfxBytes);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-path {pfxPath}",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                //result.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
                result.AllOutput.Should().Contain(_invalidPasswordErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithUntrustedSelfIssuedCertificateInCertificateStore_SuccessAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                X509Certificate2 untrustedSelfIssuedCert = _signFixture.UntrustedSelfIssuedCertificateInCertificateStore;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    pathContext.PackageSource,
                    $"nuget sign {packageFilePath} --certificate-fingerprint {untrustedSelfIssuedCert.Thumbprint}",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_chainBuildFailureErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithUnsuportedTimestampHashAlgorithm_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");
                var originalFile = File.ReadAllBytes(packageFilePath);

                ISigningTestServer testServer = await _signFixture.GetSigningTestServerAsync();
                CertificateAuthority certificateAuthority = await _signFixture.GetDefaultTrustedCertificateAuthorityAsync();
                var options = new TimestampServiceOptions() { SignatureHashAlgorithm = new Oid(Oids.Sha1) };
                TimestampService timestampService = TimestampService.Create(certificateAuthority, options);

                using (testServer.RegisterResponder(timestampService))
                using (var UntrustedSelfIssuedCert = _signFixture.UntrustedSelfIssuedCertificateInCertificateStore)
                {
                    //Act
                    var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {UntrustedSelfIssuedCert.Thumbprint} --timestamper {timestampService.Url}",
                        ignoreExitCode: true);

                    // Assert
                    result.Success.Should().BeFalse(because: result.AllOutput);
                    result.AllOutput.Should().Contain(_timestampUnsupportedDigestAlgorithmCode);
                    Assert.Contains("The timestamp signature has an unsupported digest algorithm (SHA1). The following algorithms are supported: SHA256, SHA384, SHA512.", result.AllOutput);

                    var resultingFile = File.ReadAllBytes(packageFilePath);
                    Assert.Equal(resultingFile, originalFile);
                }
            }
        }
    }
}
