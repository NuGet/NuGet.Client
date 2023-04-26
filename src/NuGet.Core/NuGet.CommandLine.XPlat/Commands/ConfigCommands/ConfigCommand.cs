// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class ConfigCommand
    {
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("config", ConfigCmd =>
            {
                ConfigCmd.Command("paths", PathsCmd =>
                {
                    CommandArgument workingdirectory = PathsCmd.Argument(
                        "WORKING_DIRECTORY",
                        Strings.ConfigPathsWorkingDirectoryDescription);
                    PathsCmd.HelpOption("-h|--help");
                    PathsCmd.Description = Strings.ConfigPathsCommandDescription;
                    PathsCmd.OnExecute(() =>
                    {
                        var args = new ConfigPathsArgs()
                        {
                            WorkingDirectory = workingdirectory.Value,
                        };

                        ConfigPathsRunner.Run(args, getLogger);
                        return 0;
                    });
                });
                ConfigCmd.Command("set", SetCmd =>
                {
                    CommandArgument configKey = SetCmd.Argument(
                        "CONFIG_KEY",
                        Strings.ConfigSetConfigKeyDescription);
                    CommandArgument configValue = SetCmd.Argument(
                        "CONFIG_VALUE",
                        Strings.ConfigSetConfigValueDescription);
                    var configFile = SetCmd.Option(
                        "--configfile",
                        Strings.ConfigSetConfigFileDescription,
                        CommandOptionType.SingleValue);
                    SetCmd.HelpOption("-h|--help");
                    SetCmd.Description = Strings.ConfigSetCommandDescription;
                    SetCmd.OnExecute(() =>
                    {
                        var args = new ConfigSetArgs()
                        {
                            ConfigKey = configKey.Value,
                            ConfigValue = configValue.Value,
                            ConfigFile = configFile.Value()
                        };

                        ConfigSetRunner.Run(args, getLogger);
                        return 0;
                    });
                });
                ConfigCmd.HelpOption("-h|--help");
                ConfigCmd.Description = Strings.Config_Description;
                ConfigCmd.OnExecute(() =>
                {
                    app.ShowHelp("config");
                    return 0;
                });
            });
        }
    }
}
