// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "locals", "LocalsCommandDescription", MinArgs = 0, MaxArgs = 1,
        UsageSummaryResourceName = "LocalsCommandSummary",
        UsageExampleResourceName = "LocalsCommandExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class LocalsCommand
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        : Command
    {
        // Default constructor used only for testing, since the Command Default Constructor is protected
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public LocalsCommand() : base()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
        }

        [Option(typeof(NuGetCommand), "LocalsCommandClearDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Clear { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "LocalsCommandListDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool List { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ILocalsCommandRunner LocalsCommandRunner { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

                return Task.CompletedTask;
            }

            if (LocalsCommandRunner == null)
            {
                LocalsCommandRunner = new LocalsCommandRunner();
            }
            var localsArgs = new LocalsArgs(Arguments, Settings, Console.LogInformation, Console.LogError, Clear, List);
            LocalsCommandRunner.ExecuteCommand(localsArgs);
            return Task.CompletedTask;
        }
    }
}
