// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class BasicLoggingTests
    {
        [Fact]
        public void BasicLogging_VersionHeading()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity",
                "verbose"
            };

            // Act
            var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(args, log);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal(1, log.VerboseMessages.Count);
            Assert.Equal(1, log.Messages.Count);
            Assert.Contains("NuGet Command Line Version:", log.ShowMessages());
        }

        [Fact]
        public void BasicLogging_VerifyExceptionLoggedWhenVerbose()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "verbose",
                "--unknown",
            };

            // Act
            var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(3, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.ShowErrors());  // error
            Assert.Contains("NuGet.CommandLine.XPlat.Program.", log.ShowMessages()); // verbose stack trace
        }

        [Fact]
        public void BasicLogging_VerifyExceptionNotLoggedLessThanVerbose()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "info",
                "--unknown",
            };

            // Act
            var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(1, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.ShowErrors());  // error
            Assert.DoesNotContain("NuGet.CommandLine.XPlat.Program.", log.ShowMessages()); // verbose stack trace
        }

        [Fact]
        public void BasicLogging_NoParams_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                //empty
            };

            // Act
            var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(args, log);

            // Assert
            Assert.Equal(0, exitCode);
        }
    }
}
