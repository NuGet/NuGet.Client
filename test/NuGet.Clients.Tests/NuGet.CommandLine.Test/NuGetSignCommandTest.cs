// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Moq;
using NuGet.Commands;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetSignCommandTest
    {
        private const string _noArgException = "No value provided for '{0}'. For a list of accepted values, please visit https://docs.nuget.org/docs/reference/command-line-reference";
        private const string _invalidArgException = "Invalid value provided for '{0}'. For a list of accepted values, please visit https://docs.nuget.org/docs/reference/command-line-reference";
        private const string _noPackageException = "No package was provided. For a list of accepted ways to provide a package, please visit https://docs.nuget.org/docs/reference/command-line-reference";
        private const string _multipleCertificateException = "Multiple options were used to specify a certificate. For a list of accepted ways to provide a certificate, please visit https://docs.nuget.org/docs/reference/command-line-reference";
        private const string _noCertificateException = "No certificate was provided. For a list of accepted ways to provide a certificate, please visit https://docs.nuget.org/docs/reference/command-line-reference";

        [Fact]
        public void SignCommandArgParsing_NoPackagePath()
        {
            // Arrange
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(_noPackageException, ex.Message);
        }

        [Fact]
        public void SignCommandArgParsing_NoTimestamper()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(string.Format(_noArgException, nameof(SignCommand.Timestamper)), ex.Message);
        }


        [Fact]
        public void SignCommandArgParsing_NoCertificate()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(_noCertificateException, ex.Message);
        }

        [Theory]
        [InlineData(@"\\foo\bar.pfx", "foo_bar_subject", "")]
        [InlineData("\\foo\bar.cert", "", "foo_bar_fingerprint")]
        [InlineData("\\foo\bar.cert", "foo_bar_subject", "foo_bar_fingerprint")]
        [InlineData("", "foo_bar_subject", "foo_bar_fingerprint")]
        public void SignCommandArgParsing_MultipleCertificateOptions(
            string certificatePath,
            string certificateSubjectName,
            string certificateFingerprint)
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath =certificatePath,
                CertificateSubjectName = certificateSubjectName,
                CertificateFingerprint = certificateFingerprint
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(_multipleCertificateException, ex.Message);
        }

        [Theory]
        [InlineData("AddressBook")]
        [InlineData("AuthRoot")]
        [InlineData("CertificateAuthority")]
        [InlineData("Disallowed")]
        [InlineData("My")]
        [InlineData("Root")]
        [InlineData("TrustedPeople")]
        [InlineData("TrustedPublisher")]
        [InlineData("AddreSSBook")]
        [InlineData("aUthrOOT")]
        [InlineData("certificateAuthority")]
        [InlineData("disAllowed")]
        [InlineData("my")]
        [InlineData("rOOt")]
        [InlineData("trustEDPeople")]
        [InlineData("truSTEDPubliSher")]
        public void SignCommandArgParsing_ValidCertificateStoreName(string storeName)
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var parsable = Enum.TryParse(storeName, ignoreCase: true, result: out StoreName parsedStoreName);
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreName = storeName
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            signCommand.Execute();
            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s => s.CertificateStoreName == parsedStoreName && s.CertificateStoreLocation == StoreLocation.CurrentUser))));
        }

        [Fact]
        public void SignCommandArgParsing_InvalidCertificateStoreName()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var storeName = "random_store";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreName = storeName
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.CertificateStoreName)), ex.Message);
        }

        [Theory]
        [InlineData("CurrentUser")]
        [InlineData("LocalMachine")]
        [InlineData("currentuser")]
        [InlineData("localmaChiNe")]
        public void SignCommandArgParsing_ValidCertificateStoreLocation(string storeLocation)
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var parsable = Enum.TryParse(storeLocation, ignoreCase: true, result: out StoreLocation parsedStoreLocation);
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreLocation = storeLocation
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            signCommand.Execute();
            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s => s.CertificateStoreLocation == parsedStoreLocation && s.CertificateStoreName == StoreName.My))));
        }

        [Fact]
        public void SignCommandArgParsing_InvalidCertificateStoreLocation()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var storeLocation = "random_location";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreLocation = storeLocation
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.CertificateStoreLocation)), ex.Message);
        }

        [Theory]
        [InlineData("sha256")]
        [InlineData("sha384")]
        [InlineData("sha512")]
        [InlineData("ShA256")]
        [InlineData("SHA256")]
        [InlineData("SHA384")]
        [InlineData("SHA512")]
        public void SignCommandArgParsing_ValidHashAlgorithm(string hashAlgorithm)
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var parsable = Enum.TryParse(hashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedHashAlgorithm);
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                HashAlgorithm = hashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            signCommand.Execute();
            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s => s.HashingAlgorithm == parsedHashAlgorithm && s.TimestampHashAlgorithm == HashAlgorithmName.SHA256))));
        }

        [Fact]
        public void SignCommandArgParsing_InvalidHashAlgorithm()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var hashAlgorithm = "MD5";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                HashAlgorithm = hashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.HashAlgorithm)), ex.Message);
        }

        [Theory]
        [InlineData("sha256")]
        [InlineData("sha384")]
        [InlineData("sha512")]
        [InlineData("ShA256")]
        [InlineData("SHA256")]
        [InlineData("SHA384")]
        [InlineData("SHA512")]
        public void SignCommandArgParsing_ValidTimestampHashAlgorithm(string timestampHashAlgorithm)
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var parsable = Enum.TryParse(timestampHashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedTimestampHashAlgorithm);
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                TimestampHashAlgorithm = timestampHashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            signCommand.Execute();
            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s => s.TimestampHashAlgorithm == parsedTimestampHashAlgorithm && s.HashingAlgorithm == HashAlgorithmName.SHA256))));
        }

        [Fact]
        public void SignCommandArgParsing_InvalidTimestampHashAlgorithm()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var timestampHashAlgorithm = "MD5";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                TimestampHashAlgorithm = timestampHashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.Execute());
            Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.TimestampHashAlgorithm)), ex.Message);
        }

        [Fact]
        public async void SignCommandArgParsing_ValidArgs_CertFingerprintAsync()
        {
            //Debugger.Launch();
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var hashAlgorithm = "sha256";
            Enum.TryParse(hashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedHashAlgorithm);
            var timestampHashAlgorithm = "sha512";
            Enum.TryParse(timestampHashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedTimestampHashAlgorithm);
            var storeName = "CertificateAuthority";
            Enum.TryParse(storeName, ignoreCase: true, result: out StoreName parsedStoreName);
            var storeLocation = "LocalMachine";
            Enum.TryParse(storeLocation, ignoreCase: true, result: out StoreLocation parsedStoreLocation);
            var nonInteractive = true;
            var overwrite = true;
            var outputDir = @".\bar";
            var csp = "csp_name";
            var kc = "kc_name";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreName = storeName,
                CertificateStoreLocation = storeLocation,
                HashAlgorithm = hashAlgorithm,
                TimestampHashAlgorithm = timestampHashAlgorithm,
                OutputDirectory = outputDir,
                NonInteractive = nonInteractive,
                Overwrite = overwrite,
                CryptographicServiceProvider = csp,
                KeyContainer = kc
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            signCommand.Execute();
            await signCommand.ExecuteCommandAsync();

            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s =>
                s.CertificatePath == null &&
                s.CertificateFingerprint == certificateFingerprint &&
                s.CertificateSubjectName == null &&
                s.CertificateStoreLocation == parsedStoreLocation &&
                s.CertificateStoreName == parsedStoreName &&
                s.CryptographicServiceProvider == csp &&
                s.KeyContainer == kc &&
                s.Logger == mockConsole.Object &&
                s.NonInteractive == nonInteractive &&
                s.Overwrite == overwrite &&
                s.PackagePath == packagePath &&
                s.Timestamper == timestamper &&
                s.OutputDirectory == outputDir))));
        }

        [Fact]
        public async void SignCommandArgParsing_ValidArgs_CertSubjectNameAsync()
        {
            //Debugger.Launch();
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateSubjectName = new Guid().ToString();
            var hashAlgorithm = "sha256";
            Enum.TryParse(hashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedHashAlgorithm);
            var timestampHashAlgorithm = "sha512";
            Enum.TryParse(timestampHashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedTimestampHashAlgorithm);
            var storeName = "CertificateAuthority";
            Enum.TryParse(storeName, ignoreCase: true, result: out StoreName parsedStoreName);
            var storeLocation = "LocalMachine";
            Enum.TryParse(storeLocation, ignoreCase: true, result: out StoreLocation parsedStoreLocation);
            var nonInteractive = true;
            var overwrite = true;
            var outputDir = @".\bar";
            var csp = "csp_name";
            var kc = "kc_name";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateSubjectName = certificateSubjectName,
                CertificateStoreName = storeName,
                CertificateStoreLocation = storeLocation,
                HashAlgorithm = hashAlgorithm,
                TimestampHashAlgorithm = timestampHashAlgorithm,
                OutputDirectory = outputDir,
                NonInteractive = nonInteractive,
                Overwrite = overwrite,
                CryptographicServiceProvider = csp,
                KeyContainer = kc
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            signCommand.Execute();
            await signCommand.ExecuteCommandAsync();

            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s =>
                s.CertificatePath == null &&
                s.CertificateFingerprint == null &&
                s.CertificateSubjectName == certificateSubjectName &&
                s.CertificateStoreLocation == parsedStoreLocation &&
                s.CertificateStoreName == parsedStoreName &&
                s.CryptographicServiceProvider == csp &&
                s.KeyContainer == kc &&
                s.Logger == mockConsole.Object &&
                s.NonInteractive == nonInteractive &&
                s.Overwrite == overwrite &&
                s.PackagePath == packagePath &&
                s.Timestamper == timestamper &&
                s.OutputDirectory == outputDir))));
        }

        [Fact]
        public async void SignCommandArgParsing_ValidArgs_CertPathAsync()
        {
            //Debugger.Launch();
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var hashAlgorithm = "sha256";
            Enum.TryParse(hashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedHashAlgorithm);
            var timestampHashAlgorithm = "sha512";
            Enum.TryParse(timestampHashAlgorithm, ignoreCase: true, result: out HashAlgorithmName parsedTimestampHashAlgorithm);
            var storeName = "My";
            Enum.TryParse(storeName, ignoreCase: true, result: out StoreName parsedStoreName);
            var storeLocation = "CurrentUser";
            Enum.TryParse(storeLocation, ignoreCase: true, result: out StoreLocation parsedStoreLocation);
            var nonInteractive = true;
            var overwrite = true;
            var outputDir = @".\bar";
            var csp = "csp_name";
            var kc = "kc_name";
            var mockSignCommandRunner = new Mock<ISignCommandRunner>();
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
                SignCommandRunner = mockSignCommandRunner.Object,
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                HashAlgorithm = hashAlgorithm,
                TimestampHashAlgorithm = timestampHashAlgorithm,
                OutputDirectory = outputDir,
                NonInteractive = nonInteractive,
                Overwrite = overwrite,
                CryptographicServiceProvider = csp,
                KeyContainer = kc
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            signCommand.Execute();
            await signCommand.ExecuteCommandAsync();

            mockSignCommandRunner.Verify(mock => mock.ExecuteCommand((It.Is<SignArgs>(s =>
                s.CertificatePath == certificatePath &&
                s.CertificateFingerprint == null &&
                s.CertificateSubjectName == null &&
                s.CertificateStoreLocation == parsedStoreLocation &&
                s.CertificateStoreName == parsedStoreName &&
                s.CryptographicServiceProvider == csp &&
                s.KeyContainer == kc &&
                s.Logger == mockConsole.Object &&
                s.NonInteractive == nonInteractive &&
                s.Overwrite == overwrite &&
                s.PackagePath == packagePath &&
                s.Timestamper == timestamper &&
                s.OutputDirectory == outputDir))));
        }
    }
}
