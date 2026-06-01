// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Common;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.XPlat.FuncTest
{
    [Collection(XPlatCollection.Name)]
    public class XPlatVerifyTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public XPlatVerifyTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void VerifyCommandArgsParsing_MissingPackagePath_LogsError()
        {
            // Arrange
            var log = new TestCommandOutputLogger(_testOutputHelper);

            // Act
            int exitCode = Program.MainInternal(new[] { "verify" }, log, TestEnvironmentVariableReader.EmptyInstance);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains("Unable to verify package. Argument '<package-paths>' not provided.", log.ShowErrors());
        }

        [Theory]
        [InlineData("-all")]
        [InlineData("-Signatures")]
        [InlineData("-certificate-fingerprint")]
        [InlineData("--h")]
        public void VerifyCommandArgsParsing_UnrecognizedOption_TreatedAsPackagePath(string unrecognizedOption)
        {
            VerifyCommandArgs(
                (mockCommandRunner, logger, rootCommand, getLogLevel) =>
                {
                    //Arrange
                    VerifyArgs capturedArgs = null!;
                    mockCommandRunner
                        .Setup(m => m.ExecuteCommandAsync(It.IsAny<VerifyArgs>()))
                        .Callback<VerifyArgs>(a => capturedArgs = a)
                        .Returns(Task.FromResult(0));
                    string[] args = new string[] { "verify", unrecognizedOption };

                    // Act
                    ParseResult parseResult = rootCommand.Parse(args);
                    parseResult.Invoke();

                    // Assert
                    // System.CommandLine treats option-like tokens that don't match a defined option
                    // as values for the package-paths argument.
                    Assert.Empty(parseResult.Errors);
                    Assert.NotNull(capturedArgs);
                    Assert.Contains(unrecognizedOption, capturedArgs.PackagePaths);
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
                (mockCommandRunner, logger, rootCommand, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string> { "verify", "packageX.nupkg", option, verbosity };

                    // Act
                    ParseResult parseResult = rootCommand.Parse(argList.ToArray());
                    int result = parseResult.Invoke();

                    // Assert
                    Assert.Equal(logLevel, getLogLevel());
                    Assert.Equal(0, result);
                });
        }

        private void VerifyCommandArgs(Action<Mock<IVerifyCommandRunner>, TestCommandOutputLogger, RootCommand, Func<LogLevel>> verify)
        {
            // Arrange
            var logLevel = LogLevel.Information;
            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var rootCommand = new RootCommand();
            var mockCommandRunner = new Mock<IVerifyCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommandAsync(It.IsAny<VerifyArgs>()))
                .Returns(Task.FromResult(0));

            VerifyCommand.Register(rootCommand,
                () => logger,
                ll => logLevel = ll,
                () => mockCommandRunner.Object);

            // Act & Assert
            verify(mockCommandRunner, logger, rootCommand, () => logLevel);
        }
    }
}
