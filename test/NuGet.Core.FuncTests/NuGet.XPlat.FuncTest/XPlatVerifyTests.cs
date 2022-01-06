// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class XPlatVerifyTests
    {
        [Fact]
        public void VerifyCommandArgsParsing_MissingPackagePath_Throws()
        {
            VerifyCommandArgs(
                (testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "verify" };

                    // Act
                    var ex = Assert.Throws<AggregateException>(() => testApp.Execute(argList.ToArray()));

                    // Assert
                    Assert.IsType<ArgumentException>(ex.InnerException);
                    Assert.Equal("Unable to verify package. Argument '<package-paths>' not provided.", ex.InnerException.Message);
                });
        }

        [Theory]
        [InlineData("-all")]
        [InlineData("-Signatures")]
        [InlineData("-certificate-fingerprint")]
        [InlineData("--h")]
        public void VerifyCommandArgsParsing_UnrcognizedOption_Throws(string unrecognizedOption)
        {
            VerifyCommandArgs(
                (testApp, getLogLevel) =>
                {
                    //Arrange
                    string[] args = new string[] { "verify", unrecognizedOption };

                    // Act & Assert
                    Assert.Throws<CommandParsingException>(() => testApp.Execute(args));
                });
        }

        [Theory]
        [InlineData("--verbosity", "q", LogLevel.Warning)]
        [InlineData("-v", "quiet", LogLevel.Warning)]
        [InlineData("--verbosity", "m", LogLevel.Minimal)]
        [InlineData("-v", "minimal", LogLevel.Minimal)]
        [InlineData("--verbosity", "something-else", LogLevel.Minimal)]
        [InlineData("-v", "n", LogLevel.Information)]
        [InlineData("--verbosity", "normal", LogLevel.Information)]
        [InlineData("-v", "d", LogLevel.Debug)]
        [InlineData("-v", "detailed", LogLevel.Debug)]
        [InlineData("--verbosity", "diag", LogLevel.Debug)]
        [InlineData("-v", "diagnostic", LogLevel.Debug)]
        public void VerifyCommandArgsParsing_VerbosityOption(string option, string verbosity, LogLevel logLevel)
        {
            using var pathContext = new SimpleTestPathContext();

            var testDirectory = pathContext.WorkingDirectory;
            var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
            var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
            File.WriteAllBytes(packageFile.FullName, package);

            VerifyCommandArgs(
                (testApp, getLogLevel) =>
                {
                    // Arrange                   
                    var argList = new List<string> { "verify", packageFile.FullName, option, verbosity };

                    // Act
                    var result = testApp.Execute(argList.ToArray());

                    // Assert
                    Assert.Equal(logLevel, getLogLevel());
                });
        }

        private void VerifyCommandArgs(Action<CommandLineApplication, Func<LogLevel>> verify)
        {
            // Arrange
            var logLevel = LogLevel.Information;
            var logger = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();

            testApp.Name = "dotnet nuget_test";
            VerifyCommand.Register(testApp,
                () => logger,
                ll => logLevel = ll);

            // Act & Assert
            verify(testApp, () => logLevel);
        }
    }
}
