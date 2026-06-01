// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Shared helpers used by the source/client-cert verb parsers that have been migrated to System.CommandLine.
    /// </summary>
    internal static class VerbCommandHelpers
    {
        // The verb runners (e.g. AddSourceRunner.Run) throw on error. Exceptions propagate to Program.MainInternal's
        // handler (System.CommandLine's default exception handler is disabled for these commands), which logs the
        // message and returns a non-zero exit code, matching the legacy parser's behavior.
        internal static int RunVerb(Action run)
        {
            run();
            return ExitCodes.Success;
        }

        internal static Argument<string> CreateNameArgument(string name, string description)
        {
            return new Argument<string>(name)
            {
                Description = description,
                // ZeroOrOne (not ExactlyOne) so a missing name yields null and the NuGet
                // runner surfaces its own command-specific error instead of System.CommandLine's
                // generic "Required argument missing" message.
                Arity = ArgumentArity.ZeroOrOne,
            };
        }

        internal static Option<string> CreateStringOption(string name, string description, string? alias = null)
        {
            Option<string> option = alias is null
                ? new Option<string>(name)
                : new Option<string>(name, alias);
            option.Description = description;
            option.Arity = ArgumentArity.ExactlyOne;
            return option;
        }

        internal static Option<bool> CreateBoolOption(string name, string description, string? alias = null)
        {
            Option<bool> option = alias is null
                ? new Option<bool>(name)
                : new Option<bool>(name, alias);
            option.Description = description;
            option.Arity = ArgumentArity.Zero;
            return option;
        }

        internal static Option<bool> CreateForceEnglishOutputOption()
        {
            return new Option<bool>(CommandConstants.ForceEnglishOutputOption)
            {
                Description = Strings.ForceEnglishOutput_Description,
                Arity = ArgumentArity.Zero,
            };
        }

        internal static HelpOption CreateHelpOption()
        {
            return new HelpOption()
            {
                Arity = ArgumentArity.Zero,
            };
        }
    }

    internal partial class AddVerbParser
    {
        // Registers a placeholder on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists `add`. The command is implemented with System.CommandLine (see overload below).
        internal static void Register(CommandLineApplication app)
        {
            app.Command("add", addCmd =>
            {
                addCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.AddSourceCommandDescription);
                addCmd.Command("client-cert", clientCertCmd => clientCertCmd.Description = Strings.AddClientCertCommandDescription);
                addCmd.Description = Strings.Add_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var addCmd = new DocumentedCommand("add", Strings.Add_Description, "https://aka.ms/dotnet/nuget/add");

            // add source
            var sourceCmd = new DocumentedCommand("source", Strings.AddSourceCommandDescription, "https://aka.ms/dotnet/nuget/add/source");
            var sourceArgument = VerbCommandHelpers.CreateNameArgument("PackageSourcePath", Strings.SourcesCommandSourceDescription);
            var name = VerbCommandHelpers.CreateStringOption("--name", Strings.SourcesCommandNameDescription, "-n");
            var username = VerbCommandHelpers.CreateStringOption("--username", Strings.SourcesCommandUsernameDescription, "-u");
            var password = VerbCommandHelpers.CreateStringOption("--password", Strings.SourcesCommandPasswordDescription, "-p");
            var storePasswordInClearText = VerbCommandHelpers.CreateBoolOption("--store-password-in-clear-text", Strings.SourcesCommandStorePasswordInClearTextDescription);
            var validAuthenticationTypes = VerbCommandHelpers.CreateStringOption("--valid-authentication-types", Strings.SourcesCommandValidAuthenticationTypesDescription);
            var protocolVersion = VerbCommandHelpers.CreateStringOption("--protocol-version", Strings.SourcesCommandProtocolVersionDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);
            var allowInsecureConnections = VerbCommandHelpers.CreateBoolOption("--allow-insecure-connections", Strings.SourcesCommandAllowInsecureConnectionsDescription);

            sourceCmd.Arguments.Add(sourceArgument);
            sourceCmd.Options.Add(name);
            sourceCmd.Options.Add(username);
            sourceCmd.Options.Add(password);
            sourceCmd.Options.Add(storePasswordInClearText);
            sourceCmd.Options.Add(validAuthenticationTypes);
            sourceCmd.Options.Add(protocolVersion);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(allowInsecureConnections);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new AddSourceArgs()
                {
                    Source = parseResult.GetValue(sourceArgument),
                    Name = parseResult.GetValue(name),
                    Username = parseResult.GetValue(username),
                    Password = parseResult.GetValue(password),
                    StorePasswordInClearText = parseResult.GetValue(storePasswordInClearText),
                    ValidAuthenticationTypes = parseResult.GetValue(validAuthenticationTypes),
                    ProtocolVersion = parseResult.GetValue(protocolVersion),
                    Configfile = parseResult.GetValue(configfile),
                    AllowInsecureConnections = parseResult.GetValue(allowInsecureConnections),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => AddSourceRunner.Run(args, getLogger)));
            });
            addCmd.Subcommands.Add(sourceCmd);

            // add client-cert
            var clientCertCmd = new DocumentedCommand("client-cert", Strings.AddClientCertCommandDescription, "https://aka.ms/dotnet/nuget/add/client-cert");
            var ccPackageSource = VerbCommandHelpers.CreateStringOption("--package-source", Strings.Option_PackageSource, "-s");
            var ccPath = VerbCommandHelpers.CreateStringOption("--path", Strings.Option_Path);
            var ccPassword = VerbCommandHelpers.CreateStringOption("--password", Strings.Option_Password);
            var ccStorePasswordInClearText = VerbCommandHelpers.CreateBoolOption("--store-password-in-clear-text", Strings.Option_StorePasswordInClearText);
            var ccStoreLocation = VerbCommandHelpers.CreateStringOption("--store-location", Strings.Option_StoreLocation);
            var ccStoreName = VerbCommandHelpers.CreateStringOption("--store-name", Strings.Option_StoreName);
            var ccFindBy = VerbCommandHelpers.CreateStringOption("--find-by", Strings.Option_FindBy);
            var ccFindValue = VerbCommandHelpers.CreateStringOption("--find-value", Strings.Option_FindValue);
            var ccForce = VerbCommandHelpers.CreateBoolOption("--force", Strings.Option_Force, "-f");
            var ccConfigfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            clientCertCmd.Options.Add(ccPackageSource);
            clientCertCmd.Options.Add(ccPath);
            clientCertCmd.Options.Add(ccPassword);
            clientCertCmd.Options.Add(ccStorePasswordInClearText);
            clientCertCmd.Options.Add(ccStoreLocation);
            clientCertCmd.Options.Add(ccStoreName);
            clientCertCmd.Options.Add(ccFindBy);
            clientCertCmd.Options.Add(ccFindValue);
            clientCertCmd.Options.Add(ccForce);
            clientCertCmd.Options.Add(ccConfigfile);
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            clientCertCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new AddClientCertArgs()
                {
                    PackageSource = parseResult.GetValue(ccPackageSource),
                    Path = parseResult.GetValue(ccPath),
                    Password = parseResult.GetValue(ccPassword),
                    StorePasswordInClearText = parseResult.GetValue(ccStorePasswordInClearText),
                    StoreLocation = parseResult.GetValue(ccStoreLocation),
                    StoreName = parseResult.GetValue(ccStoreName),
                    FindBy = parseResult.GetValue(ccFindBy),
                    FindValue = parseResult.GetValue(ccFindValue),
                    Force = parseResult.GetValue(ccForce),
                    Configfile = parseResult.GetValue(ccConfigfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => AddClientCertRunner.Run(args, getLogger)));
            });
            addCmd.Subcommands.Add(clientCertCmd);

            app.Subcommands.Add(addCmd);
        }
    }

    internal partial class DisableVerbParser
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("disable", disableCmd =>
            {
                disableCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.DisableSourceCommandDescription);
                disableCmd.Description = Strings.Disable_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var disableCmd = new DocumentedCommand("disable", Strings.Disable_Description, "https://aka.ms/dotnet/nuget/disable");

            var sourceCmd = new DocumentedCommand("source", Strings.DisableSourceCommandDescription, "https://aka.ms/dotnet/nuget/disable/source");
            var nameArgument = VerbCommandHelpers.CreateNameArgument("name", Strings.SourcesCommandNameDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            sourceCmd.Arguments.Add(nameArgument);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new DisableSourceArgs()
                {
                    Name = parseResult.GetValue(nameArgument),
                    Configfile = parseResult.GetValue(configfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => DisableSourceRunner.Run(args, getLogger)));
            });
            disableCmd.Subcommands.Add(sourceCmd);

            app.Subcommands.Add(disableCmd);
        }
    }

    internal partial class EnableVerbParser
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("enable", enableCmd =>
            {
                enableCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.EnableSourceCommandDescription);
                enableCmd.Description = Strings.Enable_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var enableCmd = new DocumentedCommand("enable", Strings.Enable_Description, "https://aka.ms/dotnet/nuget/enable");

            var sourceCmd = new DocumentedCommand("source", Strings.EnableSourceCommandDescription, "https://aka.ms/dotnet/nuget/enable/source");
            var nameArgument = VerbCommandHelpers.CreateNameArgument("name", Strings.SourcesCommandNameDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            sourceCmd.Arguments.Add(nameArgument);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new EnableSourceArgs()
                {
                    Name = parseResult.GetValue(nameArgument),
                    Configfile = parseResult.GetValue(configfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => EnableSourceRunner.Run(args, getLogger)));
            });
            enableCmd.Subcommands.Add(sourceCmd);

            app.Subcommands.Add(enableCmd);
        }
    }

    internal partial class ListVerbParser
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("list", listCmd =>
            {
                listCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.ListSourceCommandDescription);
                listCmd.Command("client-cert", clientCertCmd => clientCertCmd.Description = Strings.ListClientCertCommandDescription);
                listCmd.Description = Strings.List_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var listCmd = new DocumentedCommand("list", Strings.List_Description, "https://aka.ms/dotnet/nuget/list");

            // list source
            var sourceCmd = new DocumentedCommand("source", Strings.ListSourceCommandDescription, "https://aka.ms/dotnet/nuget/list/source");
            var format = VerbCommandHelpers.CreateStringOption("--format", Strings.SourcesCommandFormatDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            sourceCmd.Options.Add(format);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new ListSourceArgs()
                {
                    Format = parseResult.GetValue(format),
                    Configfile = parseResult.GetValue(configfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => ListSourceRunner.Run(args, getLogger)));
            });
            listCmd.Subcommands.Add(sourceCmd);

            // list client-cert
            var clientCertCmd = new DocumentedCommand("client-cert", Strings.ListClientCertCommandDescription, "https://aka.ms/dotnet/nuget/list/client-cert");
            var ccConfigfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            clientCertCmd.Options.Add(ccConfigfile);
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            clientCertCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new ListClientCertArgs()
                {
                    Configfile = parseResult.GetValue(ccConfigfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => ListClientCertRunner.Run(args, getLogger)));
            });
            listCmd.Subcommands.Add(clientCertCmd);

            app.Subcommands.Add(listCmd);
        }
    }

    internal partial class RemoveVerbParser
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("remove", removeCmd =>
            {
                removeCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.RemoveSourceCommandDescription);
                removeCmd.Command("client-cert", clientCertCmd => clientCertCmd.Description = Strings.RemoveClientCertCommandDescription);
                removeCmd.Description = Strings.Remove_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var removeCmd = new DocumentedCommand("remove", Strings.Remove_Description, "https://aka.ms/dotnet/nuget/remove");

            // remove source
            var sourceCmd = new DocumentedCommand("source", Strings.RemoveSourceCommandDescription, "https://aka.ms/dotnet/nuget/remove/source");
            var nameArgument = VerbCommandHelpers.CreateNameArgument("name", Strings.SourcesCommandNameDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            sourceCmd.Arguments.Add(nameArgument);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new RemoveSourceArgs()
                {
                    Name = parseResult.GetValue(nameArgument),
                    Configfile = parseResult.GetValue(configfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => RemoveSourceRunner.Run(args, getLogger)));
            });
            removeCmd.Subcommands.Add(sourceCmd);

            // remove client-cert
            var clientCertCmd = new DocumentedCommand("client-cert", Strings.RemoveClientCertCommandDescription, "https://aka.ms/dotnet/nuget/remove/client-cert");
            var ccPackageSource = VerbCommandHelpers.CreateStringOption("--package-source", Strings.Option_PackageSource, "-s");
            var ccConfigfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            clientCertCmd.Options.Add(ccPackageSource);
            clientCertCmd.Options.Add(ccConfigfile);
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            clientCertCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new RemoveClientCertArgs()
                {
                    PackageSource = parseResult.GetValue(ccPackageSource),
                    Configfile = parseResult.GetValue(ccConfigfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => RemoveClientCertRunner.Run(args, getLogger)));
            });
            removeCmd.Subcommands.Add(clientCertCmd);

            app.Subcommands.Add(removeCmd);
        }
    }

    internal partial class UpdateVerbParser
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("update", updateCmd =>
            {
                updateCmd.Command("source", sourceCmd => sourceCmd.Description = Strings.UpdateSourceCommandDescription);
                updateCmd.Command("client-cert", clientCertCmd => clientCertCmd.Description = Strings.UpdateClientCertCommandDescription);
                updateCmd.Description = Strings.Update_Description;
            });
        }

        internal static void Register(Command app, Func<ILogger> getLogger)
        {
            var updateCmd = new DocumentedCommand("update", Strings.Update_Description, "https://aka.ms/dotnet/nuget/update");

            // update source
            var sourceCmd = new DocumentedCommand("source", Strings.UpdateSourceCommandDescription, "https://aka.ms/dotnet/nuget/update/source");
            var nameArgument = VerbCommandHelpers.CreateNameArgument("name", Strings.SourcesCommandNameDescription);
            var source = VerbCommandHelpers.CreateStringOption("--source", Strings.SourcesCommandSourceDescription, "-s");
            var username = VerbCommandHelpers.CreateStringOption("--username", Strings.SourcesCommandUsernameDescription, "-u");
            var password = VerbCommandHelpers.CreateStringOption("--password", Strings.SourcesCommandPasswordDescription, "-p");
            var storePasswordInClearText = VerbCommandHelpers.CreateBoolOption("--store-password-in-clear-text", Strings.SourcesCommandStorePasswordInClearTextDescription);
            var validAuthenticationTypes = VerbCommandHelpers.CreateStringOption("--valid-authentication-types", Strings.SourcesCommandValidAuthenticationTypesDescription);
            var protocolVersion = VerbCommandHelpers.CreateStringOption("--protocol-version", Strings.SourcesCommandProtocolVersionDescription);
            var configfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);
            var allowInsecureConnections = VerbCommandHelpers.CreateBoolOption("--allow-insecure-connections", Strings.SourcesCommandAllowInsecureConnectionsDescription);

            sourceCmd.Arguments.Add(nameArgument);
            sourceCmd.Options.Add(source);
            sourceCmd.Options.Add(username);
            sourceCmd.Options.Add(password);
            sourceCmd.Options.Add(storePasswordInClearText);
            sourceCmd.Options.Add(validAuthenticationTypes);
            sourceCmd.Options.Add(protocolVersion);
            sourceCmd.Options.Add(configfile);
            sourceCmd.Options.Add(allowInsecureConnections);
            sourceCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            sourceCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            sourceCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new UpdateSourceArgs()
                {
                    Name = parseResult.GetValue(nameArgument),
                    Source = parseResult.GetValue(source),
                    Username = parseResult.GetValue(username),
                    Password = parseResult.GetValue(password),
                    StorePasswordInClearText = parseResult.GetValue(storePasswordInClearText),
                    ValidAuthenticationTypes = parseResult.GetValue(validAuthenticationTypes),
                    ProtocolVersion = parseResult.GetValue(protocolVersion),
                    Configfile = parseResult.GetValue(configfile),
                    AllowInsecureConnections = parseResult.GetValue(allowInsecureConnections),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => UpdateSourceRunner.Run(args, getLogger)));
            });
            updateCmd.Subcommands.Add(sourceCmd);

            // update client-cert
            var clientCertCmd = new DocumentedCommand("client-cert", Strings.UpdateClientCertCommandDescription, "https://aka.ms/dotnet/nuget/update/client-cert");
            var ccPackageSource = VerbCommandHelpers.CreateStringOption("--package-source", Strings.Option_PackageSource, "-s");
            var ccPath = VerbCommandHelpers.CreateStringOption("--path", Strings.Option_Path);
            var ccPassword = VerbCommandHelpers.CreateStringOption("--password", Strings.Option_Password);
            var ccStorePasswordInClearText = VerbCommandHelpers.CreateBoolOption("--store-password-in-clear-text", Strings.Option_StorePasswordInClearText);
            var ccStoreLocation = VerbCommandHelpers.CreateStringOption("--store-location", Strings.Option_StoreLocation);
            var ccStoreName = VerbCommandHelpers.CreateStringOption("--store-name", Strings.Option_StoreName);
            var ccFindBy = VerbCommandHelpers.CreateStringOption("--find-by", Strings.Option_FindBy);
            var ccFindValue = VerbCommandHelpers.CreateStringOption("--find-value", Strings.Option_FindValue);
            var ccForce = VerbCommandHelpers.CreateBoolOption("--force", Strings.Option_Force, "-f");
            var ccConfigfile = VerbCommandHelpers.CreateStringOption("--configfile", Strings.Option_ConfigFile);

            clientCertCmd.Options.Add(ccPackageSource);
            clientCertCmd.Options.Add(ccPath);
            clientCertCmd.Options.Add(ccPassword);
            clientCertCmd.Options.Add(ccStorePasswordInClearText);
            clientCertCmd.Options.Add(ccStoreLocation);
            clientCertCmd.Options.Add(ccStoreName);
            clientCertCmd.Options.Add(ccFindBy);
            clientCertCmd.Options.Add(ccFindValue);
            clientCertCmd.Options.Add(ccForce);
            clientCertCmd.Options.Add(ccConfigfile);
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateForceEnglishOutputOption());
            clientCertCmd.Options.Add(VerbCommandHelpers.CreateHelpOption());
            clientCertCmd.SetAction((parseResult, cancellationToken) =>
            {
                var args = new UpdateClientCertArgs()
                {
                    PackageSource = parseResult.GetValue(ccPackageSource),
                    Path = parseResult.GetValue(ccPath),
                    Password = parseResult.GetValue(ccPassword),
                    StorePasswordInClearText = parseResult.GetValue(ccStorePasswordInClearText),
                    StoreLocation = parseResult.GetValue(ccStoreLocation),
                    StoreName = parseResult.GetValue(ccStoreName),
                    FindBy = parseResult.GetValue(ccFindBy),
                    FindValue = parseResult.GetValue(ccFindValue),
                    Force = parseResult.GetValue(ccForce),
                    Configfile = parseResult.GetValue(ccConfigfile),
                };

                return Task.FromResult(VerbCommandHelpers.RunVerb(() => UpdateClientCertRunner.Run(args, getLogger)));
            });
            updateCmd.Subcommands.Add(clientCertCmd);

            app.Subcommands.Add(updateCmd);
        }
    }
}
