// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class ListVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var ListCmd = new CliCommand(name: "list", description: Strings.List_Description);

            // Options directly under the verb 'list'

            // noun sub-command: list source
            var SourceCmd = new CliCommand(name: "source", description: Strings.ListSourceCommandDescription);

            // Options under sub-command: list source
            RegisterOptionsForCommandListSource(SourceCmd, getLogger);

            ListCmd.Subcommands.Add(SourceCmd);

            app.Subcommands.Add(ListCmd);

            return ListCmd;
        }

        private static void RegisterOptionsForCommandListSource(CliCommand cmd, Func<ILogger> getLogger)
        {
            var format_Option = new CliOption<string>(name: "--format")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandFormatDescription,
            };
            cmd.Add(format_Option);
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                ListSourceArgs args = new ListSourceArgs()
                {
                    Format = parseResult.GetValue(format_Option),
                    Configfile = parseResult.GetValue(configfile_Option),
                };

                ListSourceRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
