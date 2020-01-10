using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;


namespace NuGet.CommandLine.XPlat
{

    internal partial class AddVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("add", AddCmd =>
            {
                AddCmd.Command("source", SourceCmd =>
                {
                    var Source = SourceCmd.Argument(
                        "PackageSourcePath", Strings.SourcesCommandSourceDescription);
                    var name = SourceCmd.Option(
                        "-n|--name",
                        Strings.SourcesCommandNameDescription,
                        CommandOptionType.SingleValue);
                    var username = SourceCmd.Option(
                        "-u|--username",
                        Strings.SourcesCommandUserNameDescription,
                        CommandOptionType.SingleValue);
                    var password = SourceCmd.Option(
                        "-p|--password",
                        Strings.SourcesCommandPasswordDescription,
                        CommandOptionType.SingleValue);
                    var storePasswordInClearText = SourceCmd.Option(
                        "--store-password-in-clear-text",
                        Strings.SourcesCommandStorePasswordInClearTextDescription,
                        CommandOptionType.NoValue);
                    var validAuthenticationTypes = SourceCmd.Option(
                        "--valid-authentication-types",
                        Strings.SourcesCommandValidAuthenticationTypesDescription,
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.AddSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new AddSourceArgs()
                        {
                            Source = Source.Value,
                            Name = name.Value(),
                            Username = username.Value(),
                            Password = password.Value(),
                            StorePasswordInClearText = storePasswordInClearText.HasValue(),
                            ValidAuthenticationTypes = validAuthenticationTypes.Value(),
                            Configfile = configfile.Value(),
                        };

 
                        AddSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                AddCmd.HelpOption("-h|--help");
                AddCmd.Description = Strings.Add_Description;
                AddCmd.OnExecute(() => 
                {
                    app.ShowHelp("add");
                    return 0;
                });
            });
        }
    }

    internal partial class DisableVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("disable", DisableCmd =>
            {
                DisableCmd.Command("source", SourceCmd =>
                {
                    var name = SourceCmd.Argument(
                        "name", Strings.SourcesCommandNameDescription);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.DisableSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new DisableSourceArgs()
                        {
                            Name = name.Value,
                            Configfile = configfile.Value(),
                        };

 
                        DisableSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                DisableCmd.HelpOption("-h|--help");
                DisableCmd.Description = Strings.Disable_Description;
                DisableCmd.OnExecute(() => 
                {
                    app.ShowHelp("disable");
                    return 0;
                });
            });
        }
    }

    internal partial class EnableVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("enable", EnableCmd =>
            {
                EnableCmd.Command("source", SourceCmd =>
                {
                    var name = SourceCmd.Argument(
                        "name", Strings.SourcesCommandNameDescription);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.EnableSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new EnableSourceArgs()
                        {
                            Name = name.Value,
                            Configfile = configfile.Value(),
                        };

 
                        EnableSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                EnableCmd.HelpOption("-h|--help");
                EnableCmd.Description = Strings.Enable_Description;
                EnableCmd.OnExecute(() => 
                {
                    app.ShowHelp("enable");
                    return 0;
                });
            });
        }
    }

    internal partial class ListVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("list", ListCmd =>
            {
                ListCmd.Command("source", SourceCmd =>
                {
                    var format = SourceCmd.Option(
                        "--format",
                        Strings.SourcesCommandFormatDescription,
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.ListSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new ListSourceArgs()
                        {
                            Format = format.Value(),
                            Configfile = configfile.Value(),
                        };

 
                        ListSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                ListCmd.HelpOption("-h|--help");
                ListCmd.Description = Strings.List_Description;
                ListCmd.OnExecute(() => 
                {
                    app.ShowHelp("list");
                    return 0;
                });
            });
        }
    }

    internal partial class RemoveVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("remove", RemoveCmd =>
            {
                RemoveCmd.Command("source", SourceCmd =>
                {
                    var name = SourceCmd.Argument(
                        "name", Strings.SourcesCommandNameDescription);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.RemoveSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new RemoveSourceArgs()
                        {
                            Name = name.Value,
                            Configfile = configfile.Value(),
                        };

 
                        RemoveSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                RemoveCmd.HelpOption("-h|--help");
                RemoveCmd.Description = Strings.Remove_Description;
                RemoveCmd.OnExecute(() => 
                {
                    app.ShowHelp("remove");
                    return 0;
                });
            });
        }
    }

    internal partial class UpdateVerbParser
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("update", UpdateCmd =>
            {
                UpdateCmd.Command("source", SourceCmd =>
                {
                    var name = SourceCmd.Argument(
                        "name", Strings.SourcesCommandNameDescription);
                    var source = SourceCmd.Option(
                        "-s|--source",
                        Strings.SourcesCommandSourceDescription,
                        CommandOptionType.SingleValue);
                    var username = SourceCmd.Option(
                        "-u|--username",
                        Strings.SourcesCommandUserNameDescription,
                        CommandOptionType.SingleValue);
                    var password = SourceCmd.Option(
                        "-p|--password",
                        Strings.SourcesCommandPasswordDescription,
                        CommandOptionType.SingleValue);
                    var storePasswordInClearText = SourceCmd.Option(
                        "--store-password-in-clear-text",
                        Strings.SourcesCommandStorePasswordInClearTextDescription,
                        CommandOptionType.NoValue);
                    var validAuthenticationTypes = SourceCmd.Option(
                        "--valid-authentication-types",
                        Strings.SourcesCommandValidAuthenticationTypesDescription,
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");
                    SourceCmd.Description = Strings.UpdateSourceCommandDescription;
                    SourceCmd.OnExecute(() =>
                    {
                        var args = new UpdateSourceArgs()
                        {
                            Name = name.Value,
                            Source = source.Value(),
                            Username = username.Value(),
                            Password = password.Value(),
                            StorePasswordInClearText = storePasswordInClearText.HasValue(),
                            ValidAuthenticationTypes = validAuthenticationTypes.Value(),
                            Configfile = configfile.Value(),
                        };

 
                        UpdateSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                UpdateCmd.HelpOption("-h|--help");
                UpdateCmd.Description = Strings.Update_Description;
                UpdateCmd.OnExecute(() => 
                {
                    app.ShowHelp("update");
                    return 0;
                });
            });
        }
    }

}
