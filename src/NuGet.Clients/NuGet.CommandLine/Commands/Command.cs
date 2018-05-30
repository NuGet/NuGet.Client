// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
extern alias CoreV2;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Credentials;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    public abstract class Command : ICommand
    {
        private const string CommandSuffix = "Command";
        private CommandAttribute _commandAttribute;
        private string _currentDirectory;

        protected Command()
        {
            Arguments = new List<string>();
        }

        public IList<string> Arguments { get; private set; }

        [Import]
        public IConsole Console { get; set; }

        [Import]
        public HelpCommand HelpCommand { get; set; }

        [Import]
        public ICommandManager Manager { get; set; }

        [Import]
        public Configuration.IMachineWideSettings MachineWideSettings { get; set; }

        [Option("help", AltName = "?")]
        public bool Help { get; set; }

        [Option(typeof(NuGetCommand), "Option_Verbosity")]
        public Verbosity Verbosity { get; set; }

        [Option(typeof(NuGetCommand), "Option_NonInteractive")]
        public bool NonInteractive { get; set; }

        [Option(typeof(NuGetCommand), "Option_ConfigFile")]
        public string ConfigFile { get; set; }

        [Option(typeof(NuGetCommand), "Option_ForceEnglishOutput")]
        public bool ForceEnglishOutput { get; set; }

        protected Configuration.ICredentialService CredentialService { get; private set; }

        public string CurrentDirectory
        {
            get
            {
                return _currentDirectory ?? Directory.GetCurrentDirectory();
            }
            set
            {
                _currentDirectory = value;
            }
        }

        protected internal Configuration.ISettings Settings { get; set; }

        protected internal Configuration.IPackageSourceProvider SourceProvider { get; set; }

        protected internal CoreV2.NuGet.IPackageRepositoryFactory RepositoryFactory { get; set; }

        public CommandAttribute CommandAttribute
        {
            get
            {
                if (_commandAttribute == null)
                {
                    _commandAttribute = GetCommandAttribute();
                }
                return _commandAttribute;
            }
        }

        public virtual bool IncludedInHelp(string optionName)
        {
            return true;
        }

        public void Execute()
        {
            if (Help)
            {
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);
            }
            else
            {
                if (string.IsNullOrEmpty(ConfigFile))
                {
                    string configFileName = null;

                    var packCommand = this as PackCommand;
                    if (packCommand != null && !string.IsNullOrEmpty(packCommand.ConfigFile))
                    {
                        configFileName = packCommand.ConfigFile;
                    }

                    Settings = Configuration.Settings.LoadDefaultSettings(
                        CurrentDirectory,
                        configFileName: configFileName,
                        machineWideSettings: MachineWideSettings);
                }
                else
                {
                    var configFileFullPath = Path.GetFullPath(ConfigFile);
                    var directory = Path.GetDirectoryName(configFileFullPath);
                    var configFileName = Path.GetFileName(configFileFullPath);
                    Settings = Configuration.Settings.LoadDefaultSettings(
                        directory,
                        configFileName,
                        MachineWideSettings);
                }

                SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
                SetDefaultCredentialProvider();
                RepositoryFactory = new CommandLineRepositoryFactory(Console);

                UserAgent.SetUserAgentString(new UserAgentStringBuilder(CommandLineConstants.UserAgent));

                OutputNuGetVersion();
                ExecuteCommandAsync().Wait();
            }
        }

        /// <summary>
        /// Outputs the current NuGet version (by default, only when vebosity is detailed).
        /// </summary>
        private void OutputNuGetVersion()
        {
            if (ShouldOutputNuGetVersion)
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation).FileVersion;
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("OutputNuGetVersion"),
                    assemblyName.Name,
                    version);
                Console.WriteLine(message);
            }
        }

        protected virtual bool ShouldOutputNuGetVersion
        {
            get { return Console.Verbosity == Verbosity.Detailed; }
        }

        /// <summary>
        /// Set default credential provider for the HttpClient, which is used by V2 sources.
        /// Also set up authenticated proxy handling for V3 sources.
        /// </summary>
        protected void SetDefaultCredentialProvider()
        {
            CredentialService = new CredentialService(new AsyncLazy<IEnumerable<ICredentialProvider>>(() => GetCredentialProvidersAsync()), NonInteractive);

            CoreV2.NuGet.HttpClient.DefaultCredentialProvider = new CredentialServiceAdapter(CredentialService);

            HttpHandlerResourceV3.CredentialService = new Lazy<Configuration.ICredentialService>(() => CredentialService);

            HttpHandlerResourceV3.CredentialsSuccessfullyUsed = (uri, credentials) =>
            {
                // v2 stack credentials update
                CoreV2.NuGet.CredentialStore.Instance.Add(uri, credentials);
            };
        }

        private async Task<IEnumerable<ICredentialProvider>> GetCredentialProvidersAsync()
        {
            var extensionLocator = new ExtensionLocator();
            var providers = new List<Credentials.ICredentialProvider>();
            var pluginProviders = new PluginCredentialProviderBuilder(extensionLocator, Settings, Console)
                .BuildAll(Verbosity.ToString())
                .ToList();
            var securePluginProviders =  await (new SecureCredentialProviderBuilder(PluginManager.Instance, Console)).BuildAll();

            providers.Add(new CredentialProviderAdapter(new SettingsCredentialProvider(SourceProvider, Console)));
            providers.AddRange(securePluginProviders);
            providers.AddRange(pluginProviders);

            if (pluginProviders.Any() || securePluginProviders.Any())
            {
                if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
                {
                    providers.Add(new DefaultCredentialsCredentialProvider());
                }
            }
            providers.Add(new ConsoleCredentialProvider(Console));

            return providers;
        }

        public virtual Task ExecuteCommandAsync()
        {
            ExecuteCommand();
            return Task.FromResult(0);
        }

        public virtual void ExecuteCommand()
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method does quite a bit of processing.")]
        public virtual CommandAttribute GetCommandAttribute()
        {
            var attributes = GetType().GetCustomAttributes(typeof(CommandAttribute), true);
            if (attributes.Any())
            {
                return (CommandAttribute)attributes.FirstOrDefault();
            }

            // Use the command name minus the suffix if present and default description
            var name = GetType().Name;
            var idx = name.LastIndexOf(CommandSuffix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                name = name.Substring(0, idx);
            }
            if (!string.IsNullOrEmpty(name))
            {
                return new CommandAttribute(name, LocalizedResourceManager.GetString("DefaultCommandDescription"));
            }
            return null;
        }
    }
}