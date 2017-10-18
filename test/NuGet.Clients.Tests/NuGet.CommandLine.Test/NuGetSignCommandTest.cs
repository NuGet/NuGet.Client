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
                Console = mockConsole.Object,
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
                Console = mockConsole.Object,
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
                Console = mockConsole.Object,
                Timestamper = timestamper
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath =certificatePath,
                CertificateSubjectName = certificateSubjectName,
                CertificateFingerprint = certificateFingerprint
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreName = storeName
            };
            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            var signArgs = signCommand.GetSignArgs();
            Assert.Equal(parsedStoreName, signArgs.CertificateStoreName);
            Assert.Equal(StoreLocation.CurrentUser, signArgs.CertificateStoreLocation);
        }

        [Fact]
        public void SignCommandArgParsing_InvalidCertificateStoreName()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var storeName = "random_store";
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreName = storeName
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreLocation = storeLocation
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            var signArgs = signCommand.GetSignArgs();
            Assert.Equal(StoreName.My, signArgs.CertificateStoreName);
            Assert.Equal(parsedStoreLocation, signArgs.CertificateStoreLocation);
        }

        [Fact]
        public void SignCommandArgParsing_InvalidCertificateStoreLocation()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificateFingerprint = new Guid().ToString();
            var storeLocation = "random_location";
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificateFingerprint = certificateFingerprint,
                CertificateStoreLocation = storeLocation
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                HashAlgorithm = hashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            var signArgs = signCommand.GetSignArgs();
            Assert.Equal(parsedHashAlgorithm, signArgs.HashingAlgorithm);
            Assert.Equal(HashAlgorithmName.SHA256, signArgs.TimestampHashAlgorithm);
        }

        [Fact]
        public void SignCommandArgParsing_InvalidHashAlgorithm()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var hashAlgorithm = "MD5";
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                HashAlgorithm = hashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
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
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                TimestampHashAlgorithm = timestampHashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            Assert.True(parsable);
            var signArgs = signCommand.GetSignArgs();
            Assert.Equal(HashAlgorithmName.SHA256, signArgs.HashingAlgorithm);
            Assert.Equal(parsedTimestampHashAlgorithm, signArgs.TimestampHashAlgorithm);
        }

        [Fact]
        public void SignCommandArgParsing_InvalidTimestampHashAlgorithm()
        {
            // Arrange
            var packagePath = @"foo\bar.nupkg";
            var timestamper = "http://foo.bar";
            var certificatePath = @"\\foo\bar.pfx";
            var timestampHashAlgorithm = "MD5";
            var mockConsole = new Mock<IConsole>();
            var signCommand = new SignCommand
            {
                Console = mockConsole.Object,
                Timestamper = timestamper,
                CertificatePath = certificatePath,
                TimestampHashAlgorithm = timestampHashAlgorithm
            };

            signCommand.Arguments.Add(packagePath);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => signCommand.GetSignArgs());
            Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.TimestampHashAlgorithm)), ex.Message);
        }

        [Fact]
        public void SignCommandArgParsing_ValidArgs_CertFingerprintAsync()
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
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
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
            var signArgs = signCommand.GetSignArgs();

            Assert.True(
                signArgs.CertificatePath == null &&
                signArgs.CertificateFingerprint == certificateFingerprint &&
                signArgs.CertificateSubjectName == null &&
                signArgs.CertificateStoreLocation == parsedStoreLocation &&
                signArgs.CertificateStoreName == parsedStoreName &&
                signArgs.CryptographicServiceProvider == csp &&
                signArgs.KeyContainer == kc &&
                signArgs.Logger == mockConsole.Object &&
                signArgs.NonInteractive == nonInteractive &&
                signArgs.Overwrite == overwrite &&
                signArgs.PackagePath == packagePath &&
                signArgs.Timestamper == timestamper &&
                signArgs.OutputDirectory == outputDir);
        }

        [Fact]
        public void SignCommandArgParsing_ValidArgs_CertSubjectNameAsync()
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
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
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
            var signArgs = signCommand.GetSignArgs();

            Assert.True(
                signArgs.CertificatePath == null &&
                signArgs.CertificateFingerprint == null &&
                signArgs.CertificateSubjectName == certificateSubjectName &&
                signArgs.CertificateStoreLocation == parsedStoreLocation &&
                signArgs.CertificateStoreName == parsedStoreName &&
                signArgs.CryptographicServiceProvider == csp &&
                signArgs.KeyContainer == kc &&
                signArgs.Logger == mockConsole.Object &&
                signArgs.NonInteractive == nonInteractive &&
                signArgs.Overwrite == overwrite &&
                signArgs.PackagePath == packagePath &&
                signArgs.Timestamper == timestamper &&
                signArgs.OutputDirectory == outputDir);
        }

        [Fact]
        public void SignCommandArgParsing_ValidArgs_CertPathAsync()
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
            var mockConsole = new Mock<IConsole>();
            mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);

            var signCommand = new SignCommand
            {
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
            var signArgs = signCommand.GetSignArgs();

            Assert.True(
                signArgs.CertificatePath == certificatePath &&
                signArgs.CertificateFingerprint == null &&
                signArgs.CertificateSubjectName == null &&
                signArgs.CertificateStoreLocation == parsedStoreLocation &&
                signArgs.CertificateStoreName == parsedStoreName &&
                signArgs.CryptographicServiceProvider == csp &&
                signArgs.KeyContainer == kc &&
                signArgs.Logger == mockConsole.Object &&
                signArgs.NonInteractive == nonInteractive &&
                signArgs.Overwrite == overwrite &&
                signArgs.PackagePath == packagePath &&
                signArgs.Timestamper == timestamper &&
                signArgs.OutputDirectory == outputDir);
        }
    }
}
