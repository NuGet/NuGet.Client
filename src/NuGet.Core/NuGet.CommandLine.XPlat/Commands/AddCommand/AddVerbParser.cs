// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class AddVerbParser
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var AddCmd = new CliCommand(name: "add", description: Strings.Add_Description);

            // Options directly under the verb 'add'

            // noun sub-command: add source
            var SourceCmd = new CliCommand(name: "source", description: Strings.AddSourceCommandDescription);

            // Options under sub-command: add source
            RegisterOptionsForCommandAddSource(SourceCmd, getLogger);

            AddCmd.Subcommands.Add(SourceCmd);

            // noun sub-command: add client-cert
            var ClientCertCmd = new CliCommand(name: "client-cert", description: Strings.AddClientCertCommandDescription);

            //// Options under sub-command: add client-cert
            RegisterOptionsForCommandAddClientCert(ClientCertCmd, getLogger);

            AddCmd.Subcommands.Add(ClientCertCmd);

            app.Subcommands.Add(AddCmd);

            return AddCmd;
        }

        private static void RegisterOptionsForCommandAddSource(CliCommand cmd, Func<ILogger> getLogger)
        {
            var source_Argument = new CliArgument<string>(name: "PackageSourcePath")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = Strings.SourcesCommandSourceDescription,
            };
            cmd.Add(source_Argument);
            var name_Option = new CliOption<string>(name: "--name", aliases: new[] { "-n" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SourcesCommandNameDescription,
            };
            cmd.Add(name_Option);
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
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                AddSourceArgs args = new AddSourceArgs()
                {
                    Source = parseResult.GetValue(source_Argument),
                    Name = parseResult.GetValue(name_Option),
                    Configfile = parseResult.GetValue(configfile_Option)
                };

                AddSourceRunner.Run(args, getLogger);
                return 0;
            });
        }

        private static void RegisterOptionsForCommandAddClientCert(CliCommand cmd, Func<ILogger> getLogger)
        {
            var packageSource_Option = new CliOption<string>(name: "--package-source", aliases: new[] { "-s" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_PackageSource,
            };
            cmd.Add(packageSource_Option);
            var path_Option = new CliOption<string>(name: "--path")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_Path,
            };
            cmd.Add(path_Option);
            var password_Option = new CliOption<string>(name: "--password")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_Password,
            };
            cmd.Add(password_Option);
            var storePasswordInClearText_Option = new CliOption<bool>(name: "--store-password-in-clear-text")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.Option_StorePasswordInClearText,
            };
            cmd.Add(storePasswordInClearText_Option);
            var storeLocation_Option = new CliOption<string>(name: "--store-location")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_StoreLocation,
            };
            cmd.Add(storeLocation_Option);
            var storeName_Option = new CliOption<string>(name: "--store-name")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_StoreName,
            };
            cmd.Add(storeName_Option);
            var findBy_Option = new CliOption<string>(name: "--find-by")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_FindBy,
            };
            cmd.Add(findBy_Option);
            var findValue_Option = new CliOption<string>(name: "--find-value")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_FindValue,
            };
            cmd.Add(findValue_Option);
            var force_Option = new CliOption<bool>(name: "--force", aliases: new[] { "-f" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.Option_Force
            };
            cmd.Add(force_Option);
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                AddClientCertArgs args = new AddClientCertArgs()
                {
                    PackageSource = parseResult.GetValue(packageSource_Option),
                    Path = parseResult.GetValue(path_Option),
                    Password = parseResult.GetValue(password_Option),
                    StorePasswordInClearText = parseResult.GetValue(storePasswordInClearText_Option),
                    StoreLocation = parseResult.GetValue(storeLocation_Option),
                    StoreName = parseResult.GetValue(storeName_Option),
                    FindBy = parseResult.GetValue(findBy_Option),
                    FindValue = parseResult.GetValue(findValue_Option),
                    Force = parseResult.GetValue(force_Option),
                    Configfile = parseResult.GetValue(configfile_Option),
                };

                AddClientCertRunner.Run(args, getLogger);
                return 0;
            });
        }
    }
}
