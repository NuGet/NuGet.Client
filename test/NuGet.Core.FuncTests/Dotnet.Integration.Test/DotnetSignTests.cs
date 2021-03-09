// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
#if IS_SIGNING_SUPPORTED
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
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
        private readonly string _invalidPasswordErrorCode = NuGetLogCode.NU3001.ToString();
        private readonly string _chainBuildFailureErrorCode = NuGetLogCode.NU3018.ToString();
        //Strings.SignCommandNoCertException;
        private readonly string _noCertFoundErrorCode = "No certificates were found that meet all the given criteria.";
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3002.ToString();
        private readonly string _timestampUnsupportedDigestAlgorithmCode = NuGetLogCode.NU3024.ToString();

        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public DotnetSignTests(MsbuildIntegrationTestFixture buildFixture, SignCommandTestFixture signFixture)
        {
            _msbuildFixture = buildFixture;
            _signFixture = signFixture;
            _trustedTestCert = signFixture.TrustedTestCertificate;
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

                var trustedCert = _trustedTestCert;
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
        public async Task DotnetSign_SignPackageWithInvalidEku_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var invalidEkuCert = _signFixture.TrustedTestCertificateWithInvalidEku;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {invalidEkuCert.Source.Cert.Thumbprint} --certificate-store-name {invalidEkuCert.StoreName} --certificate-store-location {invalidEkuCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse();
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

                var expiredCert = _signFixture.TrustedTestCertificateExpired;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {expiredCert.Source.Cert.Thumbprint} --certificate-store-name {expiredCert.StoreName} --certificate-store-location {expiredCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse();
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

                var notYetValidCert = _signFixture.TrustedTestCertificateNotYetValid;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {notYetValidCert.Source.Cert.Thumbprint} --certificate-store-name {notYetValidCert.StoreName} --certificate-store-location {notYetValidCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithTimestamping_SuccceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var timestampService = await _signFixture.GetDefaultTrustedTimestampServiceAsync();
                var trustedCert = _signFixture.TrustedTestCertificate;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {trustedCert.Source.Cert.Thumbprint} --certificate-store-name {trustedCert.StoreName} --certificate-store-location {trustedCert.StoreLocation} --timestamper {timestampService.Url.OriginalString}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithValidCertChain_SuccceedsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var trustedLeafCert = _signFixture.TrustedTestCertificateChain.Leaf; ;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {trustedLeafCert.Source.Cert.Thumbprint} --certificate-store-name {trustedLeafCert.StoreName} --certificate-store-location {trustedLeafCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [Fact]
        public async Task DotnetSign_SignPackageWithRevokedLeafCertChain_FailsAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    new SimpleTestPackageContext("PackageA", "1.0.0"));

                var packageFilePath = Path.Combine(pathContext.PackageSource, "PackageA.1.0.0.nupkg");

                var revokedCert = _signFixture.RevokedTestCertificateWithChain;
                //Act
                var result = _msbuildFixture.RunDotnet(
                        pathContext.PackageSource,
                        $"nuget sign {packageFilePath} --certificate-fingerprint {revokedCert.Source.Cert.Thumbprint} --certificate-store-name {revokedCert.StoreName} --certificate-store-location {revokedCert.StoreLocation}",
                        ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }
        //copied from nuget.exe
        /*

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithUnknownRevocationCertChainAsync()
        {
            // Arrange
            var cert = _testFixture.RevocationUnknownTestCertificateWithChain;
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_chainBuildFailureErrorCode);
                result.AllOutput.Should().Contain("The revocation function was unable to check revocation for the certificate");
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithOutputDirectoryAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var outputDir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packageFileName = Guid.NewGuid().ToString();
                var packagePath = Path.Combine(dir, packageFileName);
                var signedPackagePath = Path.Combine(outputDir, packageFileName);

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -OutputDirectory {outputDir}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                File.Exists(signedPackagePath).Should().BeTrue();
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_ResignPackageWithoutOverwriteFailsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                // Act
                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeFalse();
                secondResult.Errors.Should().Contain(_packageAlreadySignedError);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_ResignPackageWithOverwriteSuccessAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Overwrite",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeTrue();
                secondResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithOverwriteSuccessAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Overwrite",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithPfxFileSuccessAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -CertificatePassword {password}",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithPfxFileInteractiveSuccessAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath}",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(password);
                    });

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithPfxFileInteractiveInvalidPasswordFailsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath}",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(Guid.NewGuid().ToString());
                    });

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithPfxFileWithoutPasswordAndWithNonInteractiveFailsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -NonInteractive",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithPfxFileWithNonInteractiveAndStdInPasswordFailsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -NonInteractive",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(Guid.NewGuid().ToString());
                    });

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithUntrustedSelfIssuedCertificateInCertificateStoreAsync()
        {
            using (var directory = TestDirectory.Create())
            {
                var packageContext = new SimpleTestPackageContext();
                var packageFile = await packageContext.CreateAsFileAsync(directory, fileName: Guid.NewGuid().ToString());

                using (var certificate = _testFixture.UntrustedSelfIssuedCertificateInCertificateStore)
                {
                    var result = CommandRunner.Run(
                        _nugetExePath,
                        directory,
                        $"sign {packageFile.FullName} -CertificateFingerprint {certificate.Thumbprint}",
                        waitForExit: true);

                    Assert.True(result.Success);
                    Assert.Contains(_noTimestamperWarningCode, result.AllOutput);
                }
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithUnsuportedTimestampHashAlgorithm_ShouldNotModifyPackageAsync()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var options = new TimestampServiceOptions() { SignatureHashAlgorithm = new Oid(Oids.Sha1) };
            var timestampService = TimestampService.Create(certificateAuthority, options);

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            {
                var packageContext = new SimpleTestPackageContext();
                var packageFile = await packageContext.CreateAsFileAsync(directory, fileName: Guid.NewGuid().ToString());
                var originalFile = File.ReadAllBytes(packageFile.FullName);

                using (var certificate = _testFixture.UntrustedSelfIssuedCertificateInCertificateStore)
                {
                    var result = CommandRunner.Run(
                        _nugetExePath,
                        directory,
                        $"sign {packageFile.FullName} -CertificateFingerprint {certificate.Thumbprint} -Timestamper {timestampService.Url}",
                        waitForExit: true);

                    Assert.False(result.Success);
                    Assert.Contains(_timestampUnsupportedDigestAlgorithmCode, result.AllOutput);
                    Assert.Contains("The timestamp signature has an unsupported digest algorithm (SHA1). The following algorithms are supported: SHA256, SHA384, SHA512.", result.AllOutput);

                    var resultingFile = File.ReadAllBytes(packageFile.FullName);
                    Assert.Equal(resultingFile, originalFile);
                }
            }
        }
        */
        //end of copy
    }
}
#endif
