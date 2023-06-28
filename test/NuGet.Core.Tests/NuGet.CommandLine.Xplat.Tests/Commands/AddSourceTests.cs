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
    public class AddSourceTests
    {
        [Fact]
        public void AddSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();

            File.WriteAllText(file1, "<configuration></configuration>");
            File.WriteAllText(file2, "<configuration></configuration>");

            // Arrange
            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            AddVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            XPlat.Commands.AddVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });

            // Act
            int statusCurrent = currentCli.Execute(new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", file1 });
            int statusNew = newCli.Invoke(new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", file2 });

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
