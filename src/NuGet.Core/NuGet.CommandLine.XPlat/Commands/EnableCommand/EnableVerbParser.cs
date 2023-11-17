// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class EnableVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var EnableCmd = new CliCommand(name: "enable", description: Strings.Enable_Description);

            // Options directly under the verb 'enable'

            // noun sub-command: enable source
            var SourceCmd = new CliCommand(name: "source", description: Strings.EnableSourceCommandDescription);

            // Options under sub-command: enable source
            RegisterOptionsForCommandEnableSource(SourceCmd, getLogger);

            EnableCmd.Subcommands.Add(SourceCmd);

            app.Subcommands.Add(EnableCmd);

            return EnableCmd;
        }

        private static void RegisterOptionsForCommandEnableSource(CliCommand cmd, Func<ILogger> getLogger)
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
                EnableSourceArgs args = new EnableSourceArgs()
                {
                    Name = parseResult.GetValue(name_Argument),
                    Configfile = parseResult.GetValue(configfile_Option),
                };

                EnableSourceRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
