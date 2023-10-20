// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class ConfigCommand
    {

        internal static Func<ILogger> GetLoggerFunction;
        internal static Func<Exception, int> CommandExceptionHandler;

        internal static Command Register(Command app, Func<ILogger> getLogger, Func<Exception, int> commandExceptionHandler)
        {
            var ConfigCmd = new Command(name: "config", description: Strings.Config_Description);

            // Options directly under the verb 'config'

            // noun sub-command: config paths
            var PathsCmd = new Command(name: "paths", description: Strings.ConfigPathsCommandDescription);

            // Options under sub-command: config paths
            RegisterOptionsForCommandConfigPaths(PathsCmd, getLogger);

            ConfigCmd.AddCommand(PathsCmd);

            // noun sub-command: config get
            var GetCmd = new Command(name: "get", description: Strings.ConfigGetCommandDescription);

            // Options under sub-command: config get
            RegisterOptionsForCommandConfigGet(GetCmd, getLogger);

            ConfigCmd.AddCommand(GetCmd);

            // noun sub-command: config set
            var SetCmd = new Command(name: "set", description: Strings.ConfigSetCommandDescription);

            // Options under sub-command: config set
            RegisterOptionsForCommandConfigSet(SetCmd, getLogger);

            ConfigCmd.AddCommand(SetCmd);

            // noun sub-command: config unset
            var UnsetCmd = new Command(name: "unset", description: Strings.ConfigUnsetCommandDescription);

            // Options under sub-command: config unset
            RegisterOptionsForCommandConfigUnset(UnsetCmd, getLogger);

            ConfigCmd.AddCommand(UnsetCmd);

            GetLoggerFunction = getLogger;
            CommandExceptionHandler = commandExceptionHandler;
            app.AddCommand(ConfigCmd);

            return ConfigCmd;
        } // End noun method

        private static void RegisterOptionsForCommandConfigPaths(Command cmd, Func<ILogger> getLogger)
        {
            var workingDirectory_Option = new Option<string>(name: "--working-directory", description: Strings.ConfigPathsWorkingDirectoryDescription)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(workingDirectory_Option);
            // Create handler delegate handler for cmd
            cmd.SetHandler((args) =>
            {
                int exitCode;
                try
                {
                    ConfigPathsRunner.Run(args, getLogger);
                    exitCode = 0;
                }
                catch (Exception e)
                {
                    exitCode = CommandExceptionHandler(e);
                }
                return Task.FromResult(exitCode);
            }, new ConfigPathsCustomBinder(workingDirectory_Option));
        }

        private static void RegisterOptionsForCommandConfigGet(Command cmd, Func<ILogger> getLogger)
        {
            var allOrConfigKey_Argument = new Argument<string>(name: "all-or-config-key", description: Strings.ConfigGetAllOrConfigKeyDescription)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(allOrConfigKey_Argument);
            var workingDirectory_Argument = new Option<string>(name: "--working-directory", description: Strings.ConfigPathsWorkingDirectoryDescription)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(workingDirectory_Argument);
            var showPath_Option = new Option<bool>(name: "--show-path", description: Strings.ConfigGetShowPathDescription)
            {
                Arity = ArgumentArity.Zero,
            };
            cmd.Add(showPath_Option);
            // Create handler delegate handler for cmd
            cmd.SetHandler((args) =>
            {
                int exitCode;
                try
                {
                    ConfigGetRunner.Run(args, getLogger);
                    exitCode = 0;
                }
                catch (Exception e)
                {
                    exitCode = CommandExceptionHandler(e);
                }
                return Task.FromResult(exitCode);
            }, new ConfigGetCustomBinder(allOrConfigKey_Argument, workingDirectory_Argument, showPath_Option));
        }

        private static void RegisterOptionsForCommandConfigSet(Command cmd, Func<ILogger> getLogger)
        {
            var configKey_Argument = new Argument<string>(name: "config-key", description: Strings.ConfigSetConfigKeyDescription)
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            cmd.Add(configKey_Argument);
            var configValue_Argument = new Argument<string>(name: "config-value", description: Strings.ConfigSetConfigValueDescription)
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            cmd.Add(configValue_Argument);
            var configFile_Option = new Option<string>(name: "--configfile", description: Strings.Option_ConfigFile)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(configFile_Option);
            // Create handler delegate handler for cmd
            cmd.SetHandler((args) =>
            {
                int exitCode;
                try
                {
                    ConfigSetRunner.Run(args, getLogger);
                    exitCode = 0;
                }
                catch (Exception e)
                {
                    exitCode = CommandExceptionHandler(e);
                }
                return Task.FromResult(exitCode);
            }, new ConfigSetCustomBinder(configKey_Argument, configValue_Argument, configFile_Option));
        }

        private static void RegisterOptionsForCommandConfigUnset(Command cmd, Func<ILogger> getLogger)
        {
            var configKey_Argument = new Argument<string>(name: "config-key", description: Strings.ConfigUnsetConfigKeyDescription)
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            cmd.Add(configKey_Argument);
            var configFile_Option = new Option<string>(name: "--configfile", description: Strings.Option_ConfigFile)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(configFile_Option);
            // Create handler delegate handler for cmd
            cmd.SetHandler((args) =>
            {
                int exitCode;
                try
                {
                    ConfigUnsetRunner.Run(args, getLogger);
                    exitCode = 0;
                }
                catch (Exception e)
                {
                    exitCode = CommandExceptionHandler(e);
                }
                return Task.FromResult(exitCode);
            }, new ConfigUnsetCustomBinder(configKey_Argument, configFile_Option));
        }

        internal partial class ConfigPathsCustomBinder : BinderBase<ConfigPathsArgs>
        {
            private readonly Option<string> _workingDirectory;

            public ConfigPathsCustomBinder(Option<string> workingDirectory)
            {
                _workingDirectory = workingDirectory;
            }

            protected override ConfigPathsArgs GetBoundValue(BindingContext bindingContext)
            {
                var returnValue = new ConfigPathsArgs()
                {
                    WorkingDirectory = bindingContext.ParseResult.GetValueForOption(_workingDirectory),
                };
                return returnValue;
            }
        }

        internal partial class ConfigGetCustomBinder : BinderBase<ConfigGetArgs>
        {
            private readonly Argument<string> _allOrConfigKey;
            private readonly Option<string> _workingDirectory;
            private readonly Option<bool> _showPath;

            public ConfigGetCustomBinder(Argument<string> allOrConfigKey, Option<string> workingDirectory, Option<bool> showPath)
            {
                _allOrConfigKey = allOrConfigKey;
                _workingDirectory = workingDirectory;
                _showPath = showPath;
            }

            protected override ConfigGetArgs GetBoundValue(BindingContext bindingContext)
            {
                var returnValue = new ConfigGetArgs()
                {
                    AllOrConfigKey = bindingContext.ParseResult.GetValueForArgument(_allOrConfigKey),
                    WorkingDirectory = bindingContext.ParseResult.GetValueForOption(_workingDirectory),
                    ShowPath = bindingContext.ParseResult.GetValueForOption(_showPath),
                };
                return returnValue;
            }
        }

        internal partial class ConfigSetCustomBinder : BinderBase<ConfigSetArgs>
        {
            private readonly Argument<string> _configKey;
            private readonly Argument<string> _configValue;
            private readonly Option<string> _configFile;

            public ConfigSetCustomBinder(Argument<string> configKey, Argument<string> configValue, Option<string> configFile)
            {
                _configKey = configKey;
                _configValue = configValue;
                _configFile = configFile;
            }

            protected override ConfigSetArgs GetBoundValue(BindingContext bindingContext)
            {
                var returnValue = new ConfigSetArgs()
                {
                    ConfigKey = bindingContext.ParseResult.GetValueForArgument(_configKey),
                    ConfigValue = bindingContext.ParseResult.GetValueForArgument(_configValue),
                    ConfigFile = bindingContext.ParseResult.GetValueForOption(_configFile),
                };
                return returnValue;
            }
        }

        internal partial class ConfigUnsetCustomBinder : BinderBase<ConfigUnsetArgs>
        {
            private readonly Argument<string> _configKey;
            private readonly Option<string> _configFile;

            public ConfigUnsetCustomBinder(Argument<string> configKey, Option<string> configFile)
            {
                _configKey = configKey;
                _configFile = configFile;
            }

            protected override ConfigUnsetArgs GetBoundValue(BindingContext bindingContext)
            {
                var returnValue = new ConfigUnsetArgs()
                {
                    ConfigKey = bindingContext.ParseResult.GetValueForArgument(_configKey),
                    ConfigFile = bindingContext.ParseResult.GetValueForOption(_configFile),
                };
                return returnValue;
            }
        }
    }
}
