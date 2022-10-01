// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
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
            
            using var root1 = new SimpleTestPathContext();
            using var root2 = new SimpleTestPathContext();

            // Arrange
            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            AddVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.AddVerbParser.Register(newCli, () => testLoggerNew);

            // Act
            currentCli.Execute(new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", root1.NuGetConfig });
            newCli.Invoke(new[] { "add", "source", "https://api.nuget.org/v3/index.json", "--name", "NuGetV3", "--configfile", root2.NuGetConfig });

            // Assert
            Assert.False(testLoggerCurrent.Messages.IsEmpty);
            Assert.False(testLoggerNew.Messages.IsEmpty);
            Assert.Equal(testLoggerCurrent.Messages, testLoggerNew.Messages);

        }
    }
}
