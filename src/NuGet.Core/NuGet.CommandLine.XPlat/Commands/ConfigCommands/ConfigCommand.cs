// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class ConfigCommand
    {
        private const string WORKING_DIRECTORY = "WORKING_DIRECTORY";
        internal static void Register(CommandLineApplication app,
                                      Func<ILogger> getLogger)
        {
            app.Command("config", ConfigCmd =>
            {
                ConfigCmd.Command("paths", PathsCmd =>
                {
                    CommandArgument workingdirectory = PathsCmd.Argument(
                        WORKING_DIRECTORY,
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
                ConfigCmd.Command("get", GetCmd =>
                {
                    CommandArgument allOrConfigKey = GetCmd.Argument(
                        "<ALL|CONFIG_KEY>",
                        Strings.ConfigGetAllOrConfigKeyDescription);
                    CommandArgument workingDirectory = GetCmd.Argument(
                        WORKING_DIRECTORY,
                        Strings.ConfigPathsWorkingDirectoryDescription);
                    var showPath = GetCmd.Option(
                        "--show-path",
                        Strings.ConfigGetShowPathDescription,
                        CommandOptionType.NoValue);
                    GetCmd.HelpOption("-h|--help");
                    GetCmd.Description = Strings.ConfigGetCommandDescription;
                    GetCmd.OnExecute(() =>
                    {
                        var args = new ConfigGetArgs()
                        {
                            AllOrConfigKey = allOrConfigKey.Value,
                            WorkingDirectory = workingDirectory.Value,
                            ShowPath = showPath.HasValue()
                        };

                        ConfigGetRunner.Run(args, getLogger);
                        return 0;
                    });
                });
                ConfigCmd.Command("unset", UnsetCmd =>
                {
                    CommandArgument configKey = UnsetCmd.Argument(
                        "CONFIG_KEY",
                        Strings.ConfigUnsetConfigKeyDescription);
                    var configFile = UnsetCmd.Option(
                        "--configfile",
                        Strings.ConfigUnsetConfigFileDescription,
                        CommandOptionType.SingleValue);
                    UnsetCmd.HelpOption("-h|--help");
                    UnsetCmd.Description = Strings.ConfigUnsetCommandDescription;
                    UnsetCmd.OnExecute(() =>
                    {
                        var args = new ConfigUnsetArgs()
                        {
                            ConfigKey = configKey.Value,
                            ConfigFile = configFile.Value()
                        };
                        ConfigUnsetRunner.Run(args, getLogger);
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
