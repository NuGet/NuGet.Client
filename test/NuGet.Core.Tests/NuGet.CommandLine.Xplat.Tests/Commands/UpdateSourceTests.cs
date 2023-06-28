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
    public class UpdateSourceTests
    {
        [Fact]
        public void UpdateSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            string initialConfig = @"<configuration>
  <packageSources>
    <add key=""nugetv3"" value=""https://api.nuget.org/v3/index.json2"" />
  </packageSources>
</configuration>";
            File.WriteAllText(file1, initialConfig);
            File.WriteAllText(file2, initialConfig);

            var enableSourceCmd1 = new[] { "update", "source", "nugetv3", "--configfile", file1, "--username", "user", "--password", "pass", "--store-password-in-clear-text" };
            var enableSourceCmd2 = new[] { "update", "source", "nugetv3", "--configfile", file2, "--username", "user", "--password", "pass", "--store-password-in-clear-text" };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            UpdateVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            XPlat.Commands.UpdateVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });

            // Act
            int statusCurrent = currentCli.Execute(enableSourceCmd1);
            int statusNew = newCli.Invoke(enableSourceCmd2);

            // Assert
            CommandTestUtils.AssertEqualCommandOutput(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
            Assert.NotEqual(initialConfig, File.ReadAllText(file1));
            Assert.NotEqual(initialConfig, File.ReadAllText(file2));
        }
    }
}
