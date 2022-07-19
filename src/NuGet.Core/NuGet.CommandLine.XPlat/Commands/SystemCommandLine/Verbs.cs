
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Do not manually edit this autogenerated file:
// instead modify the neighboring .tt file (text template) and/or NuGet.CommandLine.Xplat\Commands\SystemCommandLine\Commands.xml (data file),
// then re-execute the text template via "run custom tool" on VS context menu for .tt file, or via dotnet-t4 global tool.

using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class ListVerbParser
    {
        internal static Func<ILogger> GetLoggerFunction;

        internal static Command Register(Command app, Func<ILogger> getLogger)
        {
            var ListCmd = new Command(name: "list", description: Strings.List_Description);

            // Options directly under the verb 'list'

            // noun sub-command: list source
            var SourceCmd = new Command(name: "source", description: Strings.ListSourceCommandDescription);

            // Options under sub-command: list source
            SourceCmd.AddOption(new Option<string>(name: "--format", description: Strings.SourcesCommandFormatDescription)
            {
                Arity = ArgumentArity.ZeroOrOne,
            });
            SourceCmd.AddOption(new Option<string>(name: "--configfile", description: Strings.Option_ConfigFile)
            {
                Arity = ArgumentArity.ZeroOrOne,
            });
            // Create handler delegate handler for SourceCmd
            SourceCmd.Handler = CommandHandler.Create((
                string format
                , string configfile
            ) =>
            {
                var args = new ListSourceArgs()
                {
                    Format = format,
                    Configfile = configfile,
                }; // end of args assignment

                ListSourceRunner.Run(args, getLogger);
            }); // End handler of SourceCmd

            ListCmd.AddCommand(SourceCmd);

            GetLoggerFunction = getLogger;
            app.AddCommand(ListCmd);

            return ListCmd;
        } // End noun method
    } // end class

} // end namespace
