// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;
using NuGet.Commands;
using System;

namespace NuGet.CommandLine.Test
{
    public class CommandLineParserTests
    {
        [Fact]
        public void ExtractOptions_WhenPassingOptionNoHttpCache_NoHttpCacheShouldBeTrue()
        {
            // Arrange
            RestoreCommand command = new RestoreCommand();
            List<string> args = new() { "-NoHttpCache" };
            CommandLineParser commandLineParser = new CommandLineParser(new CommandManager());

            // Act
            commandLineParser.ExtractOptions(command, args.GetEnumerator());

            // Assert
            Assert.True(command.NoHttpCache);
        }

        [Fact]
        public void ExtractOptions_WhenNotPassingOptionNoHttpCache_NoHttpCacheShouldBeFalse()
        {
            // Arrange
            RestoreCommand command = new RestoreCommand();
            List<string> args = new();
            CommandLineParser commandLineParser = new CommandLineParser(new CommandManager());

            // Act
            commandLineParser.ExtractOptions(command, args.GetEnumerator());

            // Assert
            Assert.False(command.NoHttpCache);
        }

        [Fact]
        public void ExtractOptions_WhenPassingOptionNoHttpCacheToHelpCommand_ExtractOptionsShouldRaseCommandException()
        {
            // Arrange
            DeleteCommand command = new DeleteCommand();
            string option = "-NoHttpCache";
            List<string> args = new() { option };
            CommandLineParser commandLineParser = new CommandLineParser(new CommandManager());
            CommandException expectedException = new CommandException(LocalizedResourceManager.GetString("UnknownOptionError"), option);

            // Act
            Action action = () => commandLineParser.ExtractOptions(command, args.GetEnumerator());

            // Assert
            CommandException exception = Assert.Throws<CommandException>(action);
            Assert.Equal(expectedException.Message, exception.Message);
        }
    }
}
