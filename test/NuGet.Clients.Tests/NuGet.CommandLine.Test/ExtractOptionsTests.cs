// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ExtractOptionsTests
    {

        [Fact]
        public void ExtractOption_PassOptionNoHttpCache_NoHttpCacheShouldBeSet()
        {
            //setup
            RestoreCommand command = new RestoreCommand();
            List<string> args = new() { "-NoHttpCache" };
            ICommandManager manager = new CommandManager();
            CommandLineParser commandLineParser = new CommandLineParser(manager);

            //execute extract options
            commandLineParser.ExtractOptions(command, args.GetEnumerator());

            //assert
            Assert.True(command.NoHttpCache);
        }

        [Fact]
        public void ExtractOption_DoNotPassOptionNoHttpCache_NoHttpCacheShouldNotBeSet()
        {
            //setup
            RestoreCommand command = new RestoreCommand();
            List<string> args = new() { };
            ICommandManager manager = new CommandManager();
            CommandLineParser commandLineParser = new CommandLineParser(manager);

            //execute extract options
            commandLineParser.ExtractOptions(command, args.GetEnumerator());

            //assert
            Assert.False(command.NoHttpCache);
        }

    }
}
