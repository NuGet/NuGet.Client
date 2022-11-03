// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.CommandLine.Test;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.FuncTest
{
    public class LocalizedNuGetExeTests
    {
        [CIOnlyTheory] // This test requires a localized nuget.exe build
        [InlineData("", "Restores NuGet packages.", "For more information, visit ")] // empty culture info is neutral culture
        [InlineData("pt-BR", "Restaura os pacotes NuGet.", "Para obter mais informaçoes, visite")]
        [InlineData("es-ES", "Restaura los paquetes NuGet.", "Para obtener más información, visite")]
        public void Help_WithCliLanguageEnvVarSet_ShowsHelpInDifferentLanguage(string nugetCliLanguageEnvVarValue, string expectedRestoreMessage, string expectedMoreInfoMessage)
        {
            // Arrange
            var nugetExe = Util.GetNuGetExePath();

            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_CLI_LANGUAGE", nugetCliLanguageEnvVarValue }
            };
            var args = new[] { "help" };

            // Act
            var r = CommandRunner.Run(
                process: nugetExe,
                workingDirectory: Directory.GetCurrentDirectory(),
                arguments: string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(r.ExitCode == 0, r.AllOutput);
            Assert.Contains(expectedMoreInfoMessage, r.Output);
            Assert.Contains(expectedRestoreMessage, r.Output);
        }

        [CIOnlyTheory] // This test requires a localized nuget.exe build
        [InlineData(" ")]
        [InlineData("not-a-culture-info-name")]
        public void Help_WithInvalidCliLanguageEnvVarSet_ShowsErrorMessage(string nugetCliLanguageEnvVarValue)
        {
            // Arrange
            var nugetExe = Util.GetNuGetExePath();

            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_CLI_LANGUAGE", nugetCliLanguageEnvVarValue }
            };
            var args = new[] { nugetCliLanguageEnvVarValue };

            // Act
            var r = CommandRunner.Run(
                process: nugetExe,
                workingDirectory: Directory.GetCurrentDirectory(),
                arguments: string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(r.ExitCode == 0, r.AllOutput);
            // Note: the error message will be in the configured windows language
            // In CI, we assume running in English language
            Assert.Contains($"Invalid culture identifier in NUGET_CLI_LANGUAGE environment variable. Value read is '{nugetCliLanguageEnvVarValue}'", r.AllOutput);
        }
    }
}
