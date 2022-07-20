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
            NuGet.CommandLine.XPlat.Commands.ListVerbParser.Register(newCli, () => testLoggerNew);

            // Act
            currentCli.Execute(cmd);
            newCli.Invoke(cmd);

            // Assert
            Assert.False(testLoggerCurrent.Messages.IsEmpty);
            Assert.False(testLoggerNew.Messages.IsEmpty);
            Assert.Equal(testLoggerCurrent.Messages, testLoggerNew.Messages);
        }
    }
}
