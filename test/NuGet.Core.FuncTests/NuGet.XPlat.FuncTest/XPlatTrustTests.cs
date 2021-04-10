// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatTrustTests
    {
        private static readonly string DotnetCli = DotnetCliUtil.GetDotnetCli();
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();

        [Theory]
        [InlineData("-config")]
        [InlineData("--h")]
        public void Trust_UnrecognizedOption_Fails(string unrecognizedOption)
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Arrange & Act
                //var ex = Assert.Throws<AggregateException>(() => testApp.Execute(argList.ToArray()));
                var result = CommandRunner.Run(
                      DotnetCli,
                      Directory.GetCurrentDirectory(),
                      $"{XplatDll} trust {unrecognizedOption}",
                      waitForExit: true);

                // Assert
                Assert.Equal(1, result.Item1);
                Assert.True(result.Item2.Contains($@"Specify --help for a list of available options and commands.
error: Unrecognized option '{unrecognizedOption}'"));
            }
        }

        [Theory]
        [InlineData("-v")]
        [InlineData("--algorithm")]
        [InlineData("--allow-untrusted-root")]
        [InlineData("--owners")]
        public void Trust_RecognizedOption_MissingValue_WrongCombination_Fails(string unrecognizedOption)
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Arrange & Act
                //var ex = Assert.Throws<AggregateException>(() => testApp.Execute(argList.ToArray()));
                var result = CommandRunner.Run(
                      DotnetCli,
                      Directory.GetCurrentDirectory(),
                      $"{XplatDll} trust {unrecognizedOption}",
                      waitForExit: true);

                // Assert
                Assert.Equal(1, result.Item1);
                Assert.False(string.IsNullOrEmpty(result.Item2));
            }
        }

        [Theory]
        [InlineData("trust")]
        [InlineData("trust list")]
        public static void Trust_List_Empty_Succeeds(string args)
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Arrange
                var mockPackagesDirectory = Directory.CreateDirectory(Path.Combine(mockBaseDirectory.Path, @"packages"));

                // Act
                var result = CommandRunner.Run(
                      DotnetCli,
                      mockPackagesDirectory.FullName,
                      $"{XplatDll} {args}",
                      waitForExit: true);

                // Assert
                DotnetCliUtil.VerifyResultSuccess(result, "There are no trusted signers.");
            }
        }
    }
}
