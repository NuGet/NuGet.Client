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
        // [InlineData("locals --clear all")]
        // [InlineData("locals -c all")]
        public static void Locals_Succeeds(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              Util.GetXplatDll() + " " + args,
              waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, string.Empty);
        }

        [Theory]
        [InlineData("locals")]
        [InlineData("locals --list")]
        [InlineData("locals -l")]
        [InlineData("locals --clear")]
        [InlineData("locals -c")]
        public static void Locals_Success_InvalidArguments_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              Util.GetXplatDll() + " " + args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "error: usage: NuGet locals <all | http-cache | global-packages | temp> [--clear | -c | --list | -l]" + Environment.NewLine + "error: For more information, visit http://docs.nuget.org/docs/reference/command-line-reference");
        }

        [Theory]
        [InlineData("locals --list unknownResource")]
        [InlineData("locals -l unknownResource")]
        [InlineData("locals --clear unknownResource")]
        [InlineData("locals -c unknownResource")]
        public static void Locals_Success_InvalidResourceName_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              Util.GetXplatDll() + " " + args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "error: An invalid local resource name was provided. Please provide one of the following values: http-cache, temp, global-packages, all.");
        }

        [Theory]
        [InlineData("locals -list")]
        [InlineData("locals -clear")]
        [InlineData("locals --l")]
        [InlineData("locals --c")]
        public static void Locals_Success_InvalidFlags_HelpMessage(string args)
        {
            // Act
            var result = CommandRunner.Run(
              Util.GetDotnetCli(),
              Directory.GetCurrentDirectory(),
              Util.GetXplatDll() + " " + args,
              waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "Specify --help for a list of available options and commands." + Environment.NewLine + "error: Unrecognized option '" + args.Split(null)[1] + "'");
        }
    }
}