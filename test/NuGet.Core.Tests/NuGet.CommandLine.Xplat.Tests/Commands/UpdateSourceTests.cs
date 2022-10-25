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
    public class UpdateSourceTests
    {
        [Fact]
        public void UpdateSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();

            var configFileSample = @"<configuration>
                <packageSources>
                    <add key=""src"" value=""C:\path\to\source"" />
                </packageSources>
            </configuration>";
            var newSource = @"C:\path\to\source";

            File.WriteAllText(file1, configFileSample);
            File.WriteAllText(file2, configFileSample);

            // Arrange
            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            UpdateVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.UpdateVerbParser.Register(newCli, () => testLoggerNew);

            var cmd = new[] { "update", "source", "src", "--source", newSource, "--configfile", file1 };

            // Act
            int statusCurrent = currentCli.Execute(cmd);
            int statusNew = newCli.Invoke(cmd);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
            Assert.Collection(CommandTestUtils.GetNuGetConfigSection(file2, "packageSources").Items, elem => Assert.Equal(newSource, (elem as AddItem).Value));
            Assert.Collection(CommandTestUtils.GetNuGetConfigSection(file2, "packageSources").Items, elem => Assert.Equal(newSource, (elem as AddItem).Value));
        }

    }
}
