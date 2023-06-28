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
    public class DisableSourceTests
    {
        [Fact]
        public void DisableSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            File.WriteAllText(file1, "<configuration></configuration>");
            File.WriteAllText(file2, "<configuration></configuration>");

            var prepareSourceCmd1 = new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", file1 };
            var prepareSourceCmd2 = new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", file2 };
            var cmd1 = new[] { "disable", "source", "NuGetV3", "--configfile", file1 };
            var cmd2 = new[] { "disable", "source", "NuGetV3", "--configfile", file2 };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            AddVerbParser.Register(currentCli, () => testLoggerCurrent); // needed for add command
            DisableVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            XPlat.Commands.AddVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });
            XPlat.Commands.DisableVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });

            // Arrange sources
            Assert.Equal(0, currentCli.Execute(prepareSourceCmd1));
            Assert.Equal(0, newCli.Invoke(prepareSourceCmd2));

            // Act
            int statusCurrent = currentCli.Execute(cmd1);
            int statusNew = newCli.Invoke(cmd2);

            // Assert
            CommandTestUtils.AssertEqualCommandOutput(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
