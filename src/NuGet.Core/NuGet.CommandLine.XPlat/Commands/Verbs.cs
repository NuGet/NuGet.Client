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
                    var name = SourceCmd.Option(
                        "-n|--name",
                        "",
                        CommandOptionType.SingleValue);
                    var source = SourceCmd.Option(
                        "-s|--source",
                        "",
                        CommandOptionType.SingleValue);
                    var username = SourceCmd.Option(
                        "-u|--username",
                        "",
                        CommandOptionType.SingleValue);
                    var password = SourceCmd.Option(
                        "-p|--password",
                        "",
                        CommandOptionType.SingleValue);
                    var storePasswordInClearText = SourceCmd.Option(
                        "--store-password-in-clear-text",
                        "",
                        CommandOptionType.NoValue);
                    var validAuthenticationTypes = SourceCmd.Option(
                        "--valid-authentication-types",
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

                    SourceCmd.OnExecute(() =>
                    {
                        var args = new AddSourceArgs()
                        {
                            Name = name.Value(),
                            Source = source.Value(),
                            Username = username.Value(),
                            Password = password.Value(),
                            StorePasswordInClearText = storePasswordInClearText.HasValue(),
                            ValidAuthenticationTypes = validAuthenticationTypes.Value(),
                            Configfile = configfile.Value(),
                        };

                        if (args.Name == null)
                        {
                            throw new CommandException("'name' option is missing but required.");
                        }
                        if (args.Source == null)
                        {
                            throw new CommandException("'source' option is missing but required.");
                        }
 
                        AddSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                AddCmd.HelpOption("-h|--help");
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
                    var name = SourceCmd.Option(
                        "-n|--name",
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

                    SourceCmd.OnExecute(() =>
                    {
                        var args = new DisableSourceArgs()
                        {
                            Name = name.Value(),
                            Configfile = configfile.Value(),
                        };

                        if (args.Name == null)
                        {
                            throw new CommandException("'name' option is missing but required.");
                        }
 
                        DisableSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                DisableCmd.HelpOption("-h|--help");
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
                    var name = SourceCmd.Option(
                        "-n|--name",
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

                    SourceCmd.OnExecute(() =>
                    {
                        var args = new EnableSourceArgs()
                        {
                            Name = name.Value(),
                            Configfile = configfile.Value(),
                        };

                        if (args.Name == null)
                        {
                            throw new CommandException("'name' option is missing but required.");
                        }
 
                        EnableSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                EnableCmd.HelpOption("-h|--help");
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
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

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
                    var name = SourceCmd.Option(
                        "-n|--name",
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

                    SourceCmd.OnExecute(() =>
                    {
                        var args = new RemoveSourceArgs()
                        {
                            Name = name.Value(),
                            Configfile = configfile.Value(),
                        };

                        if (args.Name == null)
                        {
                            throw new CommandException("'name' option is missing but required.");
                        }
 
                        RemoveSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                RemoveCmd.HelpOption("-h|--help");
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
                    var name = SourceCmd.Option(
                        "-n|--name",
                        "",
                        CommandOptionType.SingleValue);
                    var source = SourceCmd.Option(
                        "-s|--source",
                        "",
                        CommandOptionType.SingleValue);
                    var username = SourceCmd.Option(
                        "-n|--username",
                        "",
                        CommandOptionType.SingleValue);
                    var password = SourceCmd.Option(
                        "-s|--password",
                        "",
                        CommandOptionType.SingleValue);
                    var storePasswordInClearText = SourceCmd.Option(
                        "--store-password-in-clear-text",
                        "",
                        CommandOptionType.NoValue);
                    var validAuthenticationTypes = SourceCmd.Option(
                        "--valid-authentication-types",
                        "",
                        CommandOptionType.SingleValue);
                    var configfile = SourceCmd.Option(
                        "--configfile",
                        "",
                        CommandOptionType.SingleValue);
                    SourceCmd.HelpOption("-h|--help");

                    SourceCmd.OnExecute(() =>
                    {
                        var args = new UpdateSourceArgs()
                        {
                            Name = name.Value(),
                            Source = source.Value(),
                            Username = username.Value(),
                            Password = password.Value(),
                            StorePasswordInClearText = storePasswordInClearText.HasValue(),
                            ValidAuthenticationTypes = validAuthenticationTypes.Value(),
                            Configfile = configfile.Value(),
                        };

                        if (args.Name == null)
                        {
                            throw new CommandException("'name' option is missing but required.");
                        }
                        if (args.Source == null)
                        {
                            throw new CommandException("'source' option is missing but required.");
                        }
 
                        UpdateSourceRunner.Run(args, getLogger);
                        return 0;

                    });
                });
                UpdateCmd.HelpOption("-h|--help");
                UpdateCmd.OnExecute(() => 
                {
                    app.ShowHelp("update");
                    return 0;
                });
            });
        }
    }

}
