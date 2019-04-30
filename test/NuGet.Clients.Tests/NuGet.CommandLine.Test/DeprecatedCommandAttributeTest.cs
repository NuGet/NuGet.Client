// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    /// <summary>
    /// This test class uses two sample command implementations, located in SampleCommandExtensions project
    /// </summary>
    public class DeprecatedCommandAttributeTest
    {
        [Fact]
        public void DeprecatedCommandAttribute_GrettingCommand_AlternativeCommand_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "greet",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var output = string.Format("WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead{0}Greetings{0}", Environment.NewLine);
            Util.VerifyResultSuccess(result, output);
        }

        [Fact]
        public void DeprecatedCommandAttribute_GreetingCommand_HelpFlag_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "greet -h",
                        waitForExit: true);

            var warningMessage = "WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead";
            Util.VerifyResultSuccess(result, warningMessage);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_HelpCommand_GreetArg_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help greet",
                        waitForExit: true);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);

            var warningMessage = "WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead";
            Util.VerifyResultSuccess(result, warningMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_BeepCommand_NoAlternativeCommand_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "beep",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var output = string.Format("WARNING: 'NuGet beep' is deprecated{0}Beep{0}", Environment.NewLine);
            Util.VerifyResultSuccess(result, output);
        }

        [Fact]
        public void DeprecatedCommandAttribute_BeepCommand_HelpFlag_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "beep -h",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var beepWarningMessage = "WARNING: 'NuGet beep' is deprecated";
            Util.VerifyResultSuccess(result, beepWarningMessage);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_HelpCommand_BeepArg_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help beep",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var beepWarningMessage = "WARNING: 'NuGet beep' is deprecated";
            Util.VerifyResultSuccess(result, beepWarningMessage);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_AllCommandsFlag_WarningMessages()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help -all",
                        waitForExit: true);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);

            var warningMessage = "WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead";
            Util.VerifyResultSuccess(result, warningMessage);

            var beepWarningMessage = "WARNING: 'NuGet beep' is deprecated";
            Util.VerifyResultSuccess(result, beepWarningMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_AllCommandsFlag_MarkdownFlag_WarningMessages()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help -all -markdown",
                        waitForExit: true);

            var helpMessage = "help";
            Util.VerifyResultSuccess(result, helpMessage);

            var greetWarningMessage = "WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead";
            Util.VerifyResultSuccess(result, greetWarningMessage);

            var beepWarningMessage = "WARNING: 'NuGet beep' is deprecated";
            Util.VerifyResultSuccess(result, beepWarningMessage);
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_NoArguments_DeprecatedKeywords()
        {
            var result = CommandRunner.Run(
                                    Util.GetNuGetExePath(),
                                    Directory.GetCurrentDirectory(),
                                    "help",
                                    waitForExit: true);

            Util.VerifyResultSuccess(result, "(DEPRECATED) Prints greetings");
            Util.VerifyResultSuccess(result, "(DEPRECATED) Prints beep");
        }
    }
}
