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
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            string initialConfig = @"<configuration>
  <packageSources>
    <add key=""nugetv3"" value=""https://api.nuget.org/v3/index.json2"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""nugetv3"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            File.WriteAllText(file1, initialConfig);
            File.WriteAllText(file2, initialConfig);

            var enableSourceCmd1 = new[] { "enable", "source", "NuGetV3", "--configfile", file1 };
            var enableSourceCmd2 = new[] { "enable", "source", "NuGetV3", "--configfile", file2 };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            EnableVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            XPlat.Commands.EnableVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });

            // Act
            int statusCurrent = currentCli.Execute(enableSourceCmd1);
            int statusNew = newCli.Invoke(enableSourceCmd2);

            // Assert
            CommandTestUtils.AssertEqualCommandOutput(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
