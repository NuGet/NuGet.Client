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
        public void DeprecatedCommandAttribute_GrettingCommand_AlternativeCommand_PrintMessage()
        {
            var nugetexe = Util.GetNuGetExePath();

            var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        "greet",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var output = string.Format("WARNING: 'NuGet greet' is deprecated. Use 'NuGet hello' instead{0}Greetings{0}", Environment.NewLine);
            Util.VerifyResultSuccess(result, output);
        }

        [Fact]
        public void DeprecatedCommandAttribute_BeepCommand_NoAlternativeCommand_PrintMessage()
        {
            var nugetexe = Util.GetNuGetExePath();

            var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        "beep",
                        waitForExit: true);

            // Deprecation warning message in stdout
            var output = string.Format("WARNING: 'NuGet beep' is deprecated{0}Beep{0}", Environment.NewLine);
            Util.VerifyResultSuccess(result, output);
        }
    }
}
