// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class RemoveVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var RemoveCmd = new CliCommand(name: "remove", description: Strings.Remove_Description);

            // Options directly under the verb 'remove'

            // noun sub-command: remove source
            var SourceCmd = new CliCommand(name: "source", description: Strings.RemoveSourceCommandDescription);

            // Options under sub-command: remove source
            RegisterOptionsForCommandRemoveSource(SourceCmd, getLogger);

            RemoveCmd.Subcommands.Add(SourceCmd);

            app.Subcommands.Add(RemoveCmd);

            return RemoveCmd;
        }

        private static void RegisterOptionsForCommandRemoveSource(CliCommand cmd, Func<ILogger> getLogger)
        {
            var name_Argument = new CliArgument<string>(name: "name")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = Strings.SourcesCommandNameDescription,
            };
            cmd.Add(name_Argument);
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                RemoveSourceArgs args = new RemoveSourceArgs()
                {
                    Name = parseResult.GetValue(name_Argument),
                    Configfile = parseResult.GetValue(configfile_Option),
                };

                RemoveSourceRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
