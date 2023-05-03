// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
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

    internal static class ConfigSetRunner
    {
        public static void Run(ConfigSetArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.ValidateArguments(args, getLogger);
            RunnerHelper.ValidateConfigKey(args.ConfigKey);
            ISettings settings = string.IsNullOrEmpty(args.ConfigFile)
                ? RunnerHelper.GetSettingsFromDirectory(null)
                : RunnerHelper.GetSettingsFromFile(args.ConfigFile);

            var encrypt = false;
            if (args.ConfigKey.Equals(ConfigurationConstants.PasswordKey, StringComparison.OrdinalIgnoreCase))
            {
                encrypt = true;
            }
            SettingsUtility.SetConfigValue(settings, args.ConfigKey, args.ConfigValue, encrypt);
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
        /// Creates a settings object utilizing a NuGet configuration file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static ISettings GetSettingsFromFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            return NuGet.Configuration.Settings.LoadDefaultSettings(
                directory,
                configFileName: filePath,
                machineWideSettings: new XPlatMachineWideSetting());
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

        /// <summary>
        /// Throws an exception if the value passed in is not a valid config key.
        /// </summary>
        /// <exception cref="CommandException"></exception>
        public static void ValidateConfigKey(string configKey)
        {
            if (!configKey.Equals(ConfigurationConstants.DependencyVersion, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.GlobalPackagesFolder, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.RepositoryPath, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.DefaultPushSource, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.HostKey, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.UserKey, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.PasswordKey, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.NoProxy, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.MaxHttpRequestsPerSource, StringComparison.OrdinalIgnoreCase)
                && !configKey.Equals(ConfigurationConstants.SignatureValidationMode, StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.Error_ConfigSetInvalidKey, configKey));
            }
        }
    }
}
