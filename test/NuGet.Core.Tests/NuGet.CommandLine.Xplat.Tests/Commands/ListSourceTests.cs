// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListSourceTests
    {
        [Fact]
        public void ListSource_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            var cmd = new[] { "list", "source" };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            ListVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.ListVerbParser.Register(newCli, () => testLoggerNew, e => NuGet.CommandLine.XPlat.Program.LogException(e, testLoggerNew));

            // Act
            int statusCurrent = currentCli.Execute(cmd);
            int statusNew = newCli.Invoke(cmd);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
