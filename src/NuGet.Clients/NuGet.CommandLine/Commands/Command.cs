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
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Credentials;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.CommandLine
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class Command : ICommand
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private const string CommandSuffix = "Command";
        private CommandAttribute _commandAttribute;
        private string _currentDirectory;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Command()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            Arguments = new List<string>();
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IList<string> Arguments { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Import]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IConsole Console { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Import]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public HelpCommand HelpCommand { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Import]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICommandManager Manager { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Import]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Configuration.IMachineWideSettings MachineWideSettings { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "Option_Help", AltName = "?")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Help { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "Option_Verbosity")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Verbosity Verbosity { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "Option_NonInteractive")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NonInteractive { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "Option_ConfigFile")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ConfigFile { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "Option_ForceEnglishOutput")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool ForceEnglishOutput { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Configuration.ICredentialService CredentialService { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public DeprecatedCommandAttribute DeprecatedCommandAttribute
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                var deprecatedAttrs = GetType().GetCustomAttributes(typeof(DeprecatedCommandAttribute), false);

                if (deprecatedAttrs.Length > 0)
                {
                    return deprecatedAttrs[0] as DeprecatedCommandAttribute;
                }

                return null;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CurrentDirectory
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected internal Configuration.ISettings Settings { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected internal Configuration.IPackageSourceProvider SourceProvider { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected internal CoreV2.NuGet.IPackageRepositoryFactory RepositoryFactory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        private Lazy<MsBuildToolset> MsBuildToolset
        {
            get
            {
                if (_defaultMsBuildToolset == null)
                {
                    _defaultMsBuildToolset = MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(null, null, Console);

                }
                return _defaultMsBuildToolset;
            }
        }

        private Lazy<MsBuildToolset> _defaultMsBuildToolset;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public CommandAttribute CommandAttribute
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public virtual bool IncludedInHelp(string optionName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return true;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void Execute()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (Help)
            {
                if (DeprecatedCommandAttribute != null)
                {
                    var deprecationMessage = DeprecatedCommandAttribute.GetDeprecationMessage(CommandAttribute.CommandName);
                    Console.WriteWarning(deprecationMessage);
                }

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

                if (DeprecatedCommandAttribute != null)
                {
                    var deprecationMessage = DeprecatedCommandAttribute.GetDeprecationMessage(CommandAttribute.CommandName);
                    Console.WriteWarning(deprecationMessage);
                }

                OutputNuGetVersion();
                ExecuteCommandAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Outputs the current NuGet version (by default, only when vebosity is detailed).
        /// </summary>
        private void OutputNuGetVersion()
        {
            if (ShouldOutputNuGetVersion)
            {
                var assemblyName = typeof(Command).Assembly.GetName();
                var assemblyLocation = typeof(Command).Assembly.Location;
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation).FileVersion;
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("OutputNuGetVersion"),
                    assemblyName.Name,
                    version);
                Console.WriteLine(message);
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected virtual bool ShouldOutputNuGetVersion
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get { return Console.Verbosity == Verbosity.Detailed; }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected virtual void SetDefaultCredentialProvider()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            SetDefaultCredentialProvider(MsBuildToolset);
        }

        /// <summary>
        /// Set default credential provider for the HttpClient, which is used by V2 sources.
        /// Also set up authenticated proxy handling for V3 sources.
        /// </summary>
        protected void SetDefaultCredentialProvider(Lazy<MsBuildToolset> msbuildDirectory)
        {
            PluginDiscoveryUtility.InternalPluginDiscoveryRoot = new Lazy<string>(() => PluginDiscoveryUtility.GetInternalPluginRelativeToMSBuildDirectory(msbuildDirectory.Value.Path));
            CredentialService = new CredentialService(new AsyncLazy<IEnumerable<ICredentialProvider>>(() => GetCredentialProvidersAsync()), NonInteractive, handlesDefaultCredentials: PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders);

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
            var providers = new List<ICredentialProvider>();
            var pluginProviders = new PluginCredentialProviderBuilder(extensionLocator, Settings, Console)
                .BuildAll(Verbosity.ToString())
                .ToList();
            var securePluginProviders = await (new SecurePluginCredentialProviderBuilder(PluginManager.Instance, canShowDialog: true, logger: Console)).BuildAllAsync();

            providers.Add(new CredentialProviderAdapter(new SettingsCredentialProvider(SourceProvider, Console)));
            providers.AddRange(securePluginProviders);
            providers.AddRange(pluginProviders);

            if (pluginProviders.Any() || securePluginProviders.Any())
            {
                if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
                {
                    providers.Add(new DefaultNetworkCredentialsCredentialProvider());
                }
            }
            providers.Add(new ConsoleCredentialProvider(Console));

            return providers;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public virtual Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            ExecuteCommand();
            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public virtual void ExecuteCommand()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method does quite a bit of processing.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public virtual CommandAttribute GetCommandAttribute()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
