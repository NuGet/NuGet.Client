using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Credentials;
using System.Net;

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
            _credentialRequested = new HashSet<Uri>();
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

        // Used to check if credential has been requested for a uri. 
        private readonly HashSet<Uri> _credentialRequested;

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

        protected internal IPackageRepositoryFactory RepositoryFactory { get; set; }

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
                if (String.IsNullOrEmpty(ConfigFile))
                {
                    Settings = Configuration.Settings.LoadDefaultSettings(
                        CurrentDirectory,
                        configFileName: null,
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
                UserAgent.UserAgentString = UserAgent.CreateUserAgentString(CommandLineConstants.UserAgent);

                ExecuteCommandAsync().Wait();
            }
        }

        /// <summary>
        /// Set default credential provider for the HttpClient, which is used by V2 sources.
        /// Also set up authenticated proxy handling for V3 sources.
        /// </summary>
        protected void SetDefaultCredentialProvider()
        {
            var extensionLocator = new ExtensionLocator();
            var providers = new List<Credentials.ICredentialProvider>();
            var pluginProviders = new PluginCredentialProviderBuilder(extensionLocator, Settings).BuildAll();

            providers.Add(new CredentialProviderAdapter(new SettingsCredentialProvider(SourceProvider, Console)));
            providers.AddRange(pluginProviders);
            providers.Add(new ConsoleCredentialProvider(Console));

            var credentialService = new CredentialService(providers, Console.WriteError, NonInteractive);

            // Set up proxy handling for v3 sources.
            // We need to sync the v2 proxy cache and v3 proxy cache so that the user will not
            // get prompted twice for the same authenticated proxy.
            var v2ProxyCache = NuGet.ProxyCache.Instance as IProxyCache;
            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForProxyCredentials =
                async (uri, proxy, cancellationToken) =>
            {
                var v2Credentials = v2ProxyCache?.GetProxy(uri)?.Credentials;
                if (v2Credentials != null && proxy.Credentials != v2Credentials)
                {
                    // if cached v2 credentials have not been used, try using it first.
                    return v2Credentials;
                }

                return await credentialService.GetCredentials(
                    uri, proxy, isProxy: true, cancellationToken: cancellationToken);
            };

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.ProxyPassed = proxy =>
            {
                // add the proxy to v2 proxy cache.
                v2ProxyCache?.Add(proxy);
            };
            
            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForCredentials =
                async (uri, cancellationToken) => await credentialService.GetCredentials(
                    uri, proxy: null, isProxy: false, cancellationToken: cancellationToken);

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.CredentialsSuccessfullyUsed = (uri, credentials) =>
            {
                NuGet.CredentialStore.Instance.Add(uri, credentials);
                NuGet.Configuration.CredentialStore.Instance.Add(uri, credentials);
            };
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
            string name = GetType().Name;
            int idx = name.LastIndexOf(CommandSuffix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                name = name.Substring(0, idx);
            }
            if (!String.IsNullOrEmpty(name))
            {
                return new CommandAttribute(name, LocalizedResourceManager.GetString("DefaultCommandDescription"));
            }
            return null;
        }
    }
}