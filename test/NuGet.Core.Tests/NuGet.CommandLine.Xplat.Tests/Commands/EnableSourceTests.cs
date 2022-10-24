// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class EnableSourceTests
    {
        [Fact]
        public void EnableSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            var configFileSample = @"<configuration>
                <packageSources>
                    <add key=""src"" value=""C:\path\to\source"" />
                </packageSources>
                <disablePackageSources>
                    <add key=""src"" value=""true"" />
                </disablePackageSources>
            </configuration>";

            var configFile = Path.GetTempFileName();

            var cmd = new[] { "enable", "source", "src", "--configfile", configFile };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            EnableVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.EnableVerbParser.Register(newCli, () => testLoggerNew);

            // Act
            File.WriteAllText(configFile, configFileSample); // Prepare config file
            int statusCurrent = currentCli.Execute(cmd);
            Assert.Null(CommandTestUtils.GetNuGetConfigSection(configFile, "disabledPackageSources")?.Items);

            File.WriteAllText(configFile, configFileSample); // Reset config file
            int statusNew = newCli.Invoke(cmd);
            Assert.Null(CommandTestUtils.GetNuGetConfigSection(configFile, "disabledPackageSources")?.Items);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
