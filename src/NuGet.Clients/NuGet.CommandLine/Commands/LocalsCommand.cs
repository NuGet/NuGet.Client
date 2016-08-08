// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;


namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "locals", "LocalsCommandDescription", MinArgs = 0, MaxArgs = 1,
        UsageSummaryResourceName = "LocalsCommandSummary",
        UsageExampleResourceName = "LocalsCommandExamples")]
    public class LocalsCommand
        : Command
    {
        private const string _httpCacheResourceName = "http-cache";
        private const string _packagesCacheResourceName = "packages-cache";
        private const string _globalPackagesResourceName = "global-packages";
        private const string _tempResourceName = "temp";

        [Option(typeof(NuGetCommand), "LocalsCommandClearDescription")]
        public bool Clear { get; set; }

        [Option(typeof(NuGetCommand), "LocalsCommandListDescription")]
        public bool List { get; set; }

        public override Task ExecuteCommandAsync()
        {
            if ((!Arguments.Any() || string.IsNullOrWhiteSpace(Arguments[0]))
                || (!Clear && !List)
                || (Clear && List))
            {
                // Using both -clear and -list command options, or neither one of them, is not supported.
                // We use MinArgs = 0 even though the first argument is required,
                // to avoid throwing a command argument validation exception and
                // immediately show usage help for this command instead.
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);

                return Task.FromResult(0);
            }

            var localsCommandRunner = new LocalsCommandRunner(Arguments, Settings, Clear, List);
            localsCommandRunner.ExecuteCommand();

            return Task.FromResult(0);
        }
    }
}