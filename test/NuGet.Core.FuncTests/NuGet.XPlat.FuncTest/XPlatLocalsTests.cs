// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.CommandLine.XPlat;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatLocalsTests
    {
        [Theory]
        [InlineData("locals all --list")]
        [InlineData("locals all -l")]
        [InlineData("locals --list all")]
        [InlineData("locals -l all")]
        [InlineData("locals http-cache --list")]
        [InlineData("locals http-cache -l")]
        [InlineData("locals --list http-cache")]
        [InlineData("locals -l http-cache")]
        [InlineData("locals temp --list")]
        [InlineData("locals temp -l")]
        [InlineData("locals --list temp")]
        [InlineData("locals -l temp")]
        [InlineData("locals global-packages --list")]
        [InlineData("locals global-packages -l")]
        [InlineData("locals --list global-packages")]
        [InlineData("locals -l global-packages")]
        [InlineData("locals --clear all")]
        [InlineData("locals -c all")]
        public static void Locals_Succeeds(String args)
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act
            var exitCode = Program.MainInternal(args.Split(null), log);

            // Assert
            Assert.Equal(string.Empty, log.ShowErrors());
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("locals")]
        [InlineData("locals --list")]
        [InlineData("locals -l")]
        [InlineData("locals --clear")]
        [InlineData("locals -c")]
        public static void Locals_Success_InvalidArguments_HelpMessage(String args)
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act
            var exitCode = Program.MainInternal(args.Split(null), log);

            // Assert
            Assert.Equal("usage: NuGet locals <all | http-cache | global-packages | temp> [--clear | -c | --list | -l]" + Environment.NewLine + "For more information, visit http://docs.nuget.org/docs/reference/command-line-reference", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }

        [Theory]
        [InlineData("locals --list unknownResource")]
        [InlineData("locals -l unknownResource")]
        [InlineData("locals --clear unknownResource")]
        [InlineData("locals -c unknownResource")]
        public static void Locals_Success_InvalidResourceName_HelpMessage(String args)
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act
            var exitCode = Program.MainInternal(args.Split(null), log);

            // Assert
            Assert.Equal("An invalid local resource name was provided. Please provide one of the following values: http-cache, temp, global-packages, all.", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }

        [Theory]
        [InlineData("locals -list")]
        [InlineData("locals -clear")]
        [InlineData("locals --l")]
        [InlineData("locals --c")]
        public static void Locals_Success_InvalidFlags_HelpMessage(String args)
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act
            var exitCode = Program.MainInternal(args.Split(null), log);

            // Assert
            Assert.Equal("Unrecognized option '" + args.Split(null)[1] + "'", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }
    }
}