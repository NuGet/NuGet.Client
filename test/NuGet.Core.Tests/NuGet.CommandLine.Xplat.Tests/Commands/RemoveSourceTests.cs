// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class RemoveSourceTests
    {
        [Fact]
        public void RemoveSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            var configFileSample = @"<configuration>
                <packageSources>
                    <add key=""src"" value=""C:\path\to\source"" />
                </packageSources>
            </configuration>";

            var configFile = Path.GetTempFileName();

            var cmd = new[] { "remove", "source", "src", "--configfile", configFile };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            RemoveVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.RemoveVerbParser.Register(newCli, () => testLoggerNew, e => NuGet.CommandLine.XPlat.Program.LogException(e, testLoggerNew));

            // Act
            File.WriteAllText(configFile, configFileSample); // Prepare config file
            int statusCurrent = currentCli.Execute(cmd);
            Assert.Equal(0, CommandTestUtils.GetNuGetConfigSection(configFile, "packageSources")?.Items.Count ?? 0);

            File.WriteAllText(configFile, configFileSample); // Reset config files
            int statusNew = newCli.Invoke(cmd);
            Assert.Equal(0, CommandTestUtils.GetNuGetConfigSection(configFile, "packageSources")?.Items.Count ?? 0);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
