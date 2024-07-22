using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using NuGet.CommandLine;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.MSSigning.Extensions.Test
{
    public class NuGetMSSignCommandTest
    {
        private const string _noPackageException = "No package was provided. For a list of accepted ways to provide a package, please visit https://docs.nuget.org/docs/reference/command-line-reference";
        private const string _invalidArgException = "Invalid value provided for '{0}'. For a list of accepted values, please visit https://learn.microsoft.com/nuget/reference/cli-reference/cli-ref-sign";
        private const string _noCertificateException = "No {0} provided or provided file is not a p7b file.";
        private const string _invalidCertificateFingerprint = "Invalid value for 'CertificateFingerprint' option. The value must be a SHA-256, SHA-384, or SHA-512 certificate fingerprint (in hexadecimal).";
        private const string _sha1Hash = "89967D1DD995010B6C66AE24FF8E66885E6E03A8";

        [Fact]
        public void MSSignCommandArgParsing_NoPackagePath()
        {
            // Arrange

            using (var dir = TestDirectory.Create())
            {
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = new Guid().ToString();
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CSPName = cspName,
                    KeyContainer = keyContainer,
                    CertificateFingerprint = certificateFingerprint,
                };

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(_noPackageException, ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_NoCertificateFile()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certificateFingerprint = new Guid().ToString();
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CSPName = cspName,
                    KeyContainer = keyContainer,
                    CertificateFingerprint = certificateFingerprint,
                };
                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(string.Format(_noCertificateException, nameof(signCommand.CertificateFile)), ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_NoCSPName()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var keyContainer = new Guid().ToString();

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    KeyContainer = keyContainer,
                    CertificateFingerprint = certificateFingerprint,
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.CSPName)), ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_NoKeyContainer()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var cspName = "cert provider";

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CSPName = cspName,
                    CertificateFingerprint = certificateFingerprint,
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.KeyContainer)), ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_NoCertificateFingerprint()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CSPName = cspName,
                    KeyContainer = keyContainer,
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(_invalidCertificateFingerprint, ex.Message);
            }
        }

        [Theory]
        [InlineData("89967D1DD995010B6C66AE24FF8E66885E6E03")] // 39 characters long not SHA-1
        [InlineData("invalid-certificate-fingerprint")]
        public void MSSignCommandArgParsing_InvalidCertificateFingerprint_Throws_Exception(string certificateFingerprint)
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";

                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CertificateFingerprint = certificateFingerprint,
                    CSPName = cspName,
                    KeyContainer = keyContainer,
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(_invalidCertificateFingerprint, ex.Message);
            }
        }

        [Theory]
        [InlineData("sha256")]
        [InlineData("sha384")]
        [InlineData("sha512")]
        [InlineData("ShA256")]
        [InlineData("SHA256")]
        [InlineData("SHA384")]
        [InlineData("SHA512")]
        public void MSSignCommandArgParsing_ValidHashAlgorithm(string hashAlgorithm)
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";
                var parsable = Enum.TryParse(hashAlgorithm, ignoreCase: true, result: out Common.HashAlgorithmName parsedHashAlgorithm);
                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CertificateFingerprint = certificateFingerprint,
                    KeyContainer = keyContainer,
                    CSPName = cspName,
                    HashAlgorithm = hashAlgorithm
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                Assert.True(parsable);
                var ex = Assert.Throws<CryptographicException>(() => signCommand.GetAuthorSignRequest());
                Assert.NotEqual(string.Format(_invalidArgException, nameof(signCommand.HashAlgorithm)), ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_InvalidHashAlgorithm()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";
                var hashAlgorithm = "MD5";
                var mockConsole = new Mock<IConsole>();

                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CertificateFingerprint = certificateFingerprint,
                    KeyContainer = keyContainer,
                    CSPName = cspName,
                    HashAlgorithm = hashAlgorithm
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.HashAlgorithm)), ex.Message);
            }
        }

        [Theory]
        [InlineData("sha256")]
        [InlineData("sha384")]
        [InlineData("sha512")]
        [InlineData("ShA256")]
        [InlineData("SHA256")]
        [InlineData("SHA384")]
        [InlineData("SHA512")]
        public void MSSignCommandArgParsing_ValidTimestampHashAlgorithm(string timestampHashAlgorithm)
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";
                var parsable = Enum.TryParse(timestampHashAlgorithm, ignoreCase: true, result: out Common.HashAlgorithmName parsedHashAlgorithm);
                var mockConsole = new Mock<IConsole>();
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CertificateFingerprint = certificateFingerprint,
                    KeyContainer = keyContainer,
                    CSPName = cspName,
                    TimestampHashAlgorithm = timestampHashAlgorithm
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                Assert.True(parsable);
                var ex = Assert.Throws<CryptographicException>(() => signCommand.GetAuthorSignRequest());
                Assert.NotEqual(string.Format(_invalidArgException, nameof(signCommand.TimestampHashAlgorithm)), ex.Message);
            }
        }

        [Fact]
        public void MSSignCommandArgParsing_InvalidTimestampHashAlgorithm()
        {
            using (var dir = TestDirectory.Create())
            {
                // Arrange
                var packagePath = Path.Combine(dir, "package.nupkg");
                var timestamper = "https://timestamper.test";
                var certFile = Path.Combine(dir, "cert.p7b");
                var certificateFingerprint = _sha1Hash;
                var keyContainer = new Guid().ToString();
                var cspName = "cert provider";
                var timestampHashAlgorithm = "MD5";
                var mockConsole = new Mock<IConsole>();

                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestamper,
                    CertificateFile = certFile,
                    CertificateFingerprint = certificateFingerprint,
                    KeyContainer = keyContainer,
                    CSPName = cspName,
                    TimestampHashAlgorithm = timestampHashAlgorithm
                };

                signCommand.Arguments.Add(packagePath);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal(string.Format(_invalidArgException, nameof(signCommand.TimestampHashAlgorithm)), ex.Message);
            }
        }
    }
}
