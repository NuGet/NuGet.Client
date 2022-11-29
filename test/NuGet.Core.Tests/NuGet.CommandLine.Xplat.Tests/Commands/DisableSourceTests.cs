// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands
{
    public class DisableSourceTests
    {
        [Fact]
        public void DisableSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            var configFileSample = @"<configuration>
                <packageSources>
                    <add key=""src"" value=""C:\path\to\source"" />
                </packageSources>
            </configuration>";

            var configFile = Path.GetTempFileName();

            var cmd = new[] { "disable", "source", "src", "--configfile", configFile };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            DisableVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.DisableVerbParser.Register(newCli, () => testLoggerNew, e => NuGet.CommandLine.XPlat.Program.LogException(e, testLoggerNew));

            // Act
            File.WriteAllText(configFile, configFileSample); // Prepare config file
            int statusCurrent = currentCli.Execute(cmd);
            CommandTestUtils.AssertDisableSource(configFile, "src");

            File.WriteAllText(configFile, configFileSample); // Reset config file
            int statusNew = newCli.Invoke(cmd);
            CommandTestUtils.AssertDisableSource(configFile, "src");

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
