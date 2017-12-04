// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetVerifyCommandTest
    {
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
                Assert.Equal(1, result.Item1);
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
                Assert.Equal(1, result.Item1);
                Assert.Contains("File does not exist", result.Item3);
            }
        }
    }
}
