// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class DisableVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var DisableCmd = new CliCommand(name: "disable", description: Strings.Disable_Description);

            // Options directly under the verb 'disable'

            // noun sub-command: disable source
            var SourceCmd = new CliCommand(name: "source", description: Strings.DisableSourceCommandDescription);

            // Options under sub-command: disable source
            RegisterOptionsForCommandDisableSource(SourceCmd, getLogger);

            DisableCmd.Subcommands.Add(SourceCmd);

            app.Subcommands.Add(DisableCmd);

            return DisableCmd;
        }

        private static void RegisterOptionsForCommandDisableSource(CliCommand cmd, Func<ILogger> getLogger)
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
                DisableSourceArgs args = new DisableSourceArgs()
                {
                    Name = parseResult.GetValue(name_Argument),
                    Configfile = parseResult.GetValue(configfile_Option)
                };

                DisableSourceRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
