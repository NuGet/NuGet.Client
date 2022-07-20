// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using Xunit;
using Test.Utility;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListSourceTests
    {
        [Fact]
        public void ListSource_BothCommands_Runs()
        {
            var cmd = new[] { "list", "source" };
   
            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            ListVerbParser.Register(currentCli, () => testLoggerCurrent);
            currentCli.Execute(cmd);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.ListVerbParser.Register(newCli, () => testLoggerNew);
            newCli.Invoke(cmd);

            Assert.False(testLoggerCurrent.Messages.IsEmpty);
            Assert.False(testLoggerNew.Messages.IsEmpty);
            Assert.Equal(testLoggerCurrent.Messages, testLoggerNew.Messages);
        }
    }
}
