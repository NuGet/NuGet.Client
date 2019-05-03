// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;
using NuGet.CommandLine;

namespace NuGet.CommandLine.Test
{
    /// <summary>
    /// This test class uses two sample command implementations, located in SampleCommandExtensions project
    /// </summary>
    public class DeprecatedCommandAttributeTest
    {
        private readonly string _warning_greet_aternative = string.Format(
                LocalizedResourceManager.GetString("CommandLine_Warning"),
                string.Format(
                    LocalizedResourceManager.GetString("CommandDeprecationWarningAlternative"),
                    "NuGet", "greet", "hello"));

        private readonly string _warning_beep_simple = string.Format(
                LocalizedResourceManager.GetString("CommandLine_Warning"),
                string.Format(
                    LocalizedResourceManager.GetString("CommandDeprecationWarningSimple"),
                    "NuGet", "beep"));

        [Fact]
        public void DeprecatedCommandAttribute_GrettingCommand_AlternativeCommand_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "greet",
                        waitForExit: true);

            // Deprecation warning message in stdout
            Util.VerifyResultSuccess(result, _warning_greet_aternative);
            Util.VerifyResultSuccess(result, "Greetings");
        }

        [Fact]
        public void DeprecatedCommandAttribute_GreetingCommand_HelpFlag_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "greet -h",
                        waitForExit: true);

            Util.VerifyResultSuccess(result, _warning_greet_aternative);
            Util.VerifyResultSuccess(result, "help");
        }

        [Fact]
        public void DeprecatedCommandAttribute_HelpCommand_GreetArg_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help greet",
                        waitForExit: true);

            Util.VerifyResultSuccess(result, "help");
            Util.VerifyResultSuccess(result, _warning_greet_aternative);
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
            Util.VerifyResultSuccess(result, _warning_beep_simple);
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
            Util.VerifyResultSuccess(result, _warning_beep_simple);
            Util.VerifyResultSuccess(result, "help");
        }

        [Fact]
        public void DeprecatedCommandAttribute_HelpCommand_BeepArg_WarningMessage()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help beep",
                        waitForExit: true);

            Util.VerifyResultSuccess(result, _warning_beep_simple);
            Util.VerifyResultSuccess(result, "help");
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_AllCommandsFlag_WarningMessages()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help -all",
                        waitForExit: true);

            Util.VerifyResultSuccess(result, "help");
            Util.VerifyResultSuccess(result, _warning_greet_aternative);
            Util.VerifyResultSuccess(result, _warning_beep_simple);
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_AllCommandsFlag_MarkdownFlag_WarningMessages()
        {
            var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        "help -all -markdown",
                        waitForExit: true);

            Util.VerifyResultSuccess(result, "help");
            Util.VerifyResultSuccess(result, _warning_greet_aternative);
            Util.VerifyResultSuccess(result, _warning_beep_simple);
        }

        [Fact]
        public void DeprecatedCommandAttribute_Help_NoArguments_DeprecatedKeywords()
        {
            var result = CommandRunner.Run(
                                    Util.GetNuGetExePath(),
                                    Directory.GetCurrentDirectory(),
                                    "help",
                                    waitForExit: true);

            var deprecatedWord = LocalizedResourceManager.GetString("DeprecatedWord");
            Util.VerifyResultSuccess(result, $"{deprecatedWord} Prints greetings");
            Util.VerifyResultSuccess(result, $"{deprecatedWord} Prints beep");
        }
    }
}
