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

            if (args.AllOrConfigKey == null)
            {
                throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandKeyNotFound, args.AllOrConfigKey));
            }

            var settings = RunnerHelper.GetSettingsFromDirectory(args.WorkingDirectory) as Settings;
            if (settings == null)
            {
                return;
            }
            ILogger logger = getLogger();

            if (args.AllOrConfigKey.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var sections = settings.ComputedSections;
                if (sections == null)
                {
                    return;
                }
                if (args.ShowPath)
                {
                    RunnerHelper.LogSectionsWithPaths(sections, logger);
                }
                else
                {
                    RunnerHelper.LogSectionsNoPaths(sections, logger);
                }
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

    internal static class ConfigSetRunner
    {
        public static void Run(ConfigSetArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.ValidateArguments(args, getLogger);
            if (string.IsNullOrEmpty(args.ConfigFile))
            {
                var settingsFromDirectory = RunnerHelper.GetSettingsFromDirectory(null);
                SettingsUtility.SetConfigValue(settingsFromDirectory, args.ConfigKey, args.ConfigValue);
            }
            else
            {
                var settings = RunnerHelper.GetSettingsFromFile(args.ConfigFile);
                SettingsUtility.SetConfigValue(settings, args.ConfigKey, args.ConfigValue);
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

        public static ISettings GetSettingsFromFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            return NuGet.Configuration.Settings.LoadDefaultSettings(
                directory,
                configFileName: filePath,
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

            SettingSection sectionElement = settings.GetSection(ConfigurationConstants.Config);
            AddItem item = sectionElement?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, key);

            if (item == null)
            {
                return null;
            }

            if (showPath)
            {
                return item.Value + "\tfile: " + item.ConfigPath;
            }
            return item.Value;
        }

        /// <summary>
        /// Logs each section of the configuration settings that will be applied, grouped by file path.
        /// </summary>
        public static void LogSectionsWithPaths(Dictionary<string, VirtualSettingSection> sections, ILogger logger)
        {
            foreach (var section in sections)
            {
                logger.LogMinimal(section.Key + ":");
                var items = section.Value.Items;

                IEnumerable<IGrouping<string, SettingItem>> groupByConfigPathsQuery =
                    from item in items
                    group item by item.ConfigPath into newItemGroup
                    select newItemGroup;

                foreach (var configPathsGroup in groupByConfigPathsQuery)
                {
                    logger.LogMinimal($" file: {configPathsGroup.Key}");
                    LogSectionItems(configPathsGroup, logger);
                    logger.LogMinimal("\n");
                }
            }
        }

        /// <summary>
        /// Logs each section of the configuration settings that will be applied.
        /// </summary>
        public static void LogSectionsNoPaths(Dictionary<string, VirtualSettingSection> sections, ILogger logger)
        {
            foreach (var section in sections)
            {
                logger.LogMinimal(section.Key + ":");
                var items = section.Value.Items;
                LogSectionItems(items, logger);
                logger.LogMinimal("\n");
            }
        }

        /// <summary>
        /// Combines the attributes from each item in a collection of SettingItems into a string, then logs the string.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="logger"></param>
        public static void LogSectionItems(IEnumerable<SettingItem> items, ILogger logger)
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

        /// <summary>
        /// Throws an exception if any of the config runner arguments are null.
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
