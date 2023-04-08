// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal static class ConfigPathsRunner
    {
        public static void Run(ConfigPathsArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.ValidateArguments(args, getLogger);

            if (string.IsNullOrEmpty(args.WorkingDirectory))
            {
                args.WorkingDirectory = Directory.GetCurrentDirectory();
            }

            var settings = RunnerHelper.GetSettingsFromDirectory(args.WorkingDirectory);
            ILogger logger = getLogger();

            var filePaths = settings.GetConfigFilePaths();
            foreach (var filePath in filePaths)
            {
                logger.LogMinimal(filePath);
            }
        }
    }

    internal static class ConfigGetRunner
    {
        public static void Run(ConfigGetArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.ValidateArguments(args, getLogger);

            var settings = RunnerHelper.GetSettingsFromDirectory(args.WorkingDirectory);
            ILogger logger = getLogger();

            if (args.AllOrConfigKey.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var sections = settings.GetComputedSections();
                RunnerHelper.LogSections(sections, logger, args.ShowPath);
            }
            else
            {
                var configValue = RunnerHelper.GetValueForConfigKey(settings, args.AllOrConfigKey, args.ShowPath);
                if (string.IsNullOrEmpty(configValue))
                {
                    throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandKeyNotFound, args.AllOrConfigKey));
                }

                logger.LogMinimal(configValue);
            }
        }
    }

    internal static class RunnerHelper
    {
        /// <summary>
        /// Creates a settings object using the directory argument,
        /// or using the current directory if no argument is passed.
        /// </summary>
        /// <exception cref="CommandException"></exception>
        public static ISettings GetSettingsFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }
            if (!Directory.Exists(directory))
            {
                throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PathNotFound, directory));
            }

            return NuGet.Configuration.Settings.LoadDefaultSettings(
                directory,
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
        }

        /// <summary>
        /// Returns a string holding the value of the key in the config section
        /// of the settings. If showPath is true, this will also return the path
        /// to the configuration file where the key is located.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public static string GetValueForConfigKey(ISettings settings, string key, bool showPath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var sectionElement = settings.GetSection(ConfigurationConstants.Config);
            var item = sectionElement?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, key);

            if (item == null)
            {
                return null;
            }

            if (showPath)
            {
                return item.Value + "\tfile: " + item.GetConfigFilePath();
            }
            return item.Value;
        }

        public static void LogSections(Dictionary<string, VirtualSettingSection> sections, ILogger logger, bool showPath)
        {
            foreach (var section in sections)
            {
                logger.LogMinimal(section.Key + ":");
                var items = section.Value.Items;

                if (showPath)
                {
                    var groupByConfigPathsQuery =
                        from item in items
                        group item by item.GetConfigPath() into newItemGroup
                        orderby newItemGroup.Key
                        select newItemGroup;

                    foreach (var configPathsGroup in groupByConfigPathsQuery)
                    {
                        logger.LogMinimal($" file: {configPathsGroup.Key}");
                        foreach (var item in configPathsGroup)
                        {
                            var setting = $"\t{item.ElementName}";
                            var attributes = item.GetXElementAttributes();
                            foreach (var attribute in attributes)
                            {
                                setting += " " + attribute + " ";
                            }
                            logger.LogMinimal(setting);
                        }
                    }
                }
                else
                {
                    foreach (var item in items)
                    {
                        var setting = $"\t{item.ElementName}";
                        var attributes = item.GetXElementAttributes();
                        foreach (var attribute in attributes)
                        {
                            setting += " " + attribute + " ";
                        }
                        logger.LogMinimal(setting);
                    }
                }
                logger.LogMinimal("\n");
            }
        }

        /// <summary>
        /// Throws an exception if any of the arguments for the config runners are null.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public static void ValidateArguments<TArgs>(TArgs args, Func<ILogger> logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
        }
    }
}
