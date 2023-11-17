// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class UpdateVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var UpdateCmd = new CliCommand(name: "update", description: Strings.Update_Description);

            // Options directly under the verb 'update'

            // noun sub-command: update source
            var SourceCmd = new CliCommand(name: "source", description: Strings.UpdateSourceCommandDescription);

            // Options under sub-command: update source
            RegisterOptionsForCommandUpdateSource(SourceCmd, getLogger);

            UpdateCmd.Subcommands.Add(SourceCmd);

            app.Subcommands.Add(UpdateCmd);

            return UpdateCmd;
        }

        private static void RegisterOptionsForCommandUpdateSource(CliCommand cmd, Func<ILogger> getLogger)
        {
            var name_Argument = new CliArgument<string>(name: "name")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = Strings.SourcesCommandNameDescription,
            };
            cmd.Add(name_Argument);
            var source_Option = new CliOption<string>(name: "--source", aliases: new[] { "-s" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandSourceDescription,
            };
            cmd.Add(source_Option);
            var username_Option = new CliOption<string>(name: "--username", aliases: new[] { "-u" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandUsernameDescription,
            };
            cmd.Add(username_Option);
            var password_Option = new CliOption<string>(name: "--password", aliases: new[] { "-p" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandPasswordDescription,
            };
            cmd.Add(password_Option);
            var storePasswordInClearText_Option = new CliOption<bool>(name: "--store-password-in-clear-text")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.SourcesCommandStorePasswordInClearTextDescription,
            };
            cmd.Add(storePasswordInClearText_Option);
            var validAuthenticationTypes_Option = new CliOption<string>(name: "--valid-authentication-types")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandValidAuthenticationTypesDescription,
            };
            cmd.Add(validAuthenticationTypes_Option);
            var protocolVersion_Option = new CliOption<string>(name: "--protocol-version")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandProtocolVersionDescription,
            };
            cmd.Add(validAuthenticationTypes_Option);
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                UpdateSourceArgs args = new UpdateSourceArgs()
                {
                    Name = parseResult.GetValue(name_Argument),
                    Source = parseResult.GetValue(source_Option),
                    Username = parseResult.GetValue(username_Option),
                    Password = parseResult.GetValue(password_Option),
                    StorePasswordInClearText = parseResult.GetValue(storePasswordInClearText_Option),
                    ValidAuthenticationTypes = parseResult.GetValue(validAuthenticationTypes_Option),
                    ProtocolVersion = parseResult.GetValue(protocolVersion_Option),
                    Configfile = parseResult.GetValue(configfile_Option),
                };

                UpdateSourceRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
