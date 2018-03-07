// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetVerifyCommandTest
    {
        private const int _failureCode = 1;
        private const int _successCode = 0;

        [Fact]
        public void VerifyCommand_VerifyUnknownVerificationType()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", packageFileName };
                var result = CommandRunner.Run(
                    nugetexe,
                    packageDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(_failureCode, result.Item1);
                Assert.Contains("Verification type not supported.", result.Item3);
            }
        }

        [Fact]
        public void VerifyCommand_WrongInput_NotFound()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Act
                var args = new string[] { "verify", "-Signatures", "testPackage1" };
                var result = CommandRunner.Run(
                    nugetexe,
                    packageDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(_failureCode, result.Item1);
                Assert.Contains("File does not exist", result.Item3);
            }
        }

        [Fact]
        public void VerifyCommand_WithAuthorSignedPackage_FailsGracefully()
        {
            var nugetExe = Util.GetNuGetExePath();

            using (var directory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(directory.Path, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = GetResource(packageFile.Name);

                File.WriteAllBytes(packageFile.FullName, package);

                var args = new string[] { "verify", "-Signatures", packageFile.Name };
                var result = CommandRunner.Run(
                    nugetExe,
                    packageFile.Directory.FullName,
                    string.Join(" ", args),
                    waitForExit: true);

                if (RuntimeEnvironmentHelper.IsMono)
                {
                    Assert.Equal(_failureCode, result.Item1);
                    Assert.Equal(_failureCode, result.ExitCode);
                    Assert.False(result.Success);
                    Assert.Contains("The package signature is invalid or cannot be verified on this platform.", result.Output);
                }
                else
                {
                    Assert.Equal(_successCode, result.Item1);
                    Assert.Equal(_successCode, result.ExitCode);
                    Assert.True(result.Success);
                    Assert.Contains("Successfully verified package(s).", result.Output);
                }
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.CommandLine.Test.compiler.resources.{name}",
                typeof(NuGetVerifyCommandTest));
        }
    }
}