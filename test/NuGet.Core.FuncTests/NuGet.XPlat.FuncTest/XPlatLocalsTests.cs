// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatLocalsTests
    {
        [Theory]
        [InlineData("nuget locals all --list")]
        [InlineData("nuget locals all -l")]
        [InlineData("nuget locals --list all")]
        [InlineData("nuget locals -l all")]
        [InlineData("nuget locals http-cache --list")]
        [InlineData("nuget locals http-cache -l")]
        [InlineData("nuget locals --list http-cache")]
        [InlineData("nuget locals -l http-cache")]
        [InlineData("nuget locals temp --list")]
        [InlineData("nuget locals temp -l")]
        [InlineData("nuget locals --list temp")]
        [InlineData("nuget locals -l temp")]
        [InlineData("nuget locals global-packages --list")]
        [InlineData("nuget locals global-packages -l")]
        [InlineData("nuget locals --list global-packages")]
        [InlineData("nuget locals -l global-packages")]
        // [InlineData("nuget locals --clear all")]
        // [InlineData("nuget locals -c all")]
        public static void Locals_Succeeds(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              args,
              waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, string.Empty);
        }

        [Theory]
        [InlineData("nuget locals")]
        [InlineData("nuget locals --list")]
        [InlineData("nuget locals -l")]
        [InlineData("nuget locals --clear")]
        [InlineData("nuget locals -c")]
        public static void Locals_Success_InvalidArguments_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "error: usage: NuGet locals <all | http-cache | global-packages | temp> [--clear | -c | --list | -l]" + Environment.NewLine + "error: For more information, visit http://docs.nuget.org/docs/reference/command-line-reference");
        }

        [Theory]
        [InlineData("nuget locals --list unknownResource")]
        [InlineData("nuget locals -l unknownResource")]
        [InlineData("nuget locals --clear unknownResource")]
        [InlineData("nuget locals -c unknownResource")]
        public static void Locals_Success_InvalidResourceName_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "error: An invalid local resource name was provided. Please provide one of the following values: http-cache, temp, global-packages, all.");
        }

        [Theory]
        [InlineData("nuget locals -list")]
        [InlineData("nuget locals -clear")]
        [InlineData("nuget locals --l")]
        [InlineData("nuget locals --c")]
        public static void Locals_Success_InvalidFlags_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "Specify --help for a list of available options and commands." + Environment.NewLine + "error: Unrecognized option '" + args.Split(null)[2] + "'");
        }
    }
}