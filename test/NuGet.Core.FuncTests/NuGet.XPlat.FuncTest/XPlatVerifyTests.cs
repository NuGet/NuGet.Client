// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Common;
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
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "verify" };

                    // Act
                    var ex = Assert.Throws<AggregateException>(() => testApp.Execute(argList.ToArray()));

                    // Assert
                    Assert.IsType<ArgumentException>(ex.InnerException);
                    Assert.Equal("Unable to verify package. Argument '<package-paths>' not provided.", ex.InnerException.Message);
                });

            VerifyCommandArgs2(
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "verify" };

                    // Act
                    var a = testApp.Parse(argList.ToArray());

                    var ex = Assert.Throws<ArgumentException>(() => a.Action.Invoke(a));

                    // Assert
                    Assert.Equal("Unable to verify package. Argument '<package-paths>' not provided.", ex.Message);
                });
            VerifyCommandArgs2(
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    //Arrange
                    string[] args = new string[] { "verify", "-all" };

                    // Act & Assert
                    var a = testApp.Parse(args);
                    Assert.Throws<CommandParsingException>(() => a.Action.Invoke(a));
                });
        }

        //[Theory]
        //[InlineData("-all")]
        //[InlineData("-Signatures")]
        //[InlineData("-certificate-fingerprint")]
        //[InlineData("--h")]
        [Fact]
        public void VerifyCommandArgsParsing_UnrcognizedOption_Throws()
        {
            VerifyCommandArgs(
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    //Arrange
                    string[] args = new string[] { "verify", "-all" };

                    // Act & Assert
                    Assert.Throws<CommandParsingException>(() => testApp.Execute(args));
                });
            VerifyCommandArgs2(
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    //Arrange
                    string[] args = new string[] { "verify", "-all" };

                    // Act & Assert
                    var a = testApp.Parse(args);
                    Assert.Throws<CommandParsingException>(() => a.Action.Invoke(a));
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
            VerifyCommandArgs(
                (mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange                   
                    var argList = new List<string> { "verify", "packageX.nupkg", option, verbosity };

                    // Act
                    var result = 0;

                    // Assert
                    Assert.Equal(logLevel, getLogLevel());
                    Assert.Equal(0, result);
                });
        }

        private void VerifyCommandArgs(Action<Mock<IVerifyCommandRunner>, CommandLineApplication, Func<LogLevel>> verify)
        {
            // Arrange
            var logLevel = LogLevel.Information;
            var logger = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();
            var mockCommandRunner = new Mock<IVerifyCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommandAsync(It.IsAny<VerifyArgs>()))
                .Returns(Task.FromResult(0));

            testApp.Name = "dotnet nuget_test";
            VerifyCommand.Register(testApp,
                () => logger,
                ll => logLevel = ll,
                () => mockCommandRunner.Object);

            // Act & Assert
            verify(mockCommandRunner, testApp, () => logLevel);
        }

        private void VerifyCommandArgs2(Action<Mock<IVerifyCommandRunner>, CliRootCommand, Func<LogLevel>> verify)
        {
            // Arrange
            var logLevel = LogLevel.Information;
            var logger = new TestCommandOutputLogger();
            var testApp = new CliRootCommand();
            testApp.TreatUnmatchedTokensAsErrors = true;
            var mockCommandRunner = new Mock<IVerifyCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommandAsync(It.IsAny<VerifyArgs>()))
                .Returns(Task.FromResult(0));

            VerifyCommand.Register(testApp,
                () => logger,
                ll => logLevel = ll,
                () => mockCommandRunner.Object);

            // Act & Assert
            verify(mockCommandRunner, testApp, () => logLevel);
        }
    }
}
