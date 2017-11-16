using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    class NuGetVerifyCommandTest
    {
        // ************************ Test invalid inputs

        [Fact]
        public void VerifyCommand_VerifyUnknownVerificationType()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }

        [Fact]
        public void VerifyCommand_WrongInput_NotFound()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Act
                var args = new string[] { "verify", "-Signatures", "testPackage1" };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }


        [Fact]
        public void VerifyCommand_VerifyUnsignedPackage()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }

        // ************************ Test correct cases

        [Fact]
        public void VerifyCommand_VerifySignedPackage()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                // TODO: Assert message
            }
        }

        [Fact]
        public void VerifyCommand_SignedPackageWithTimestamp()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with timestamp
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                // TODO: Assert message
            }
        }

        // ************************ Test errors

        [Fact]
        public void VerifyCommand_SignedPackage_SignatureVersionNotSupported()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with unsupported version
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }

        [Fact]
        public void VerifyCommand_SignedPackage_HashAlgorithmNotSupported()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with unsupported hash Algorithm
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }

        [Fact]
        public void VerifyCommand_SignedPackage_Tampered()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with tampered contents
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(1, result.Item1);
                // TODO: Assert error
            }
        }

        // ************************ Test warnings

        [Fact]
        public void VerifyCommand_SignedPackageWithTimestamp_UntrustedTimestamp()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with untrusted timestamp
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                // TODO: Assert warning
            }
        }


        [Fact]
        public void VerifyCommand_UntrustedCertificate()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package with untrusted certificate
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                // TODO: Assert warning
            }
        }

        [Fact]
        public void VerifyCommand_RevocationStatusUnavailable()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Arrange
                // TODO: Create signed package
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // TODO: Have a way to test that revocation status check was not possible (offline scenario)

                // Act
                var args = new string[] { "verify", "-Signatures", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);
                // TODO: Assert warning
            }
        }
    }
}
