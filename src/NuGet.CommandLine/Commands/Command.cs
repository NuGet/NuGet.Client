using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Common;
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

        private HashSet<Uri> _credentialRequested;

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
            // Register an additional provider for the console specific application so that the user
            // will be prompted if a proxy is set and credentials are required
            var credentialProvider = new SettingsCredentialProvider(
                new ConsoleCredentialProvider(Console),
                SourceProvider,
                Console);
            HttpClient.DefaultCredentialProvider = credentialProvider;

            // Set up proxy handling for v3 sources.
            // We need to sync the v2 proxy cache and v3 proxy cache so that the user will not
            // get prompted twice for the same authenticated proxy.
            var v2ProxyCache = NuGet.ProxyCache.Instance as IProxyCache;
            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForProxyCredentials = (uri, proxy) =>
            {
                var v2Credentials = v2ProxyCache?.GetProxy(uri)?.Credentials;
                if (v2Credentials != null && proxy.Credentials != v2Credentials)
                {
                    // if cached v2 credentials have not been used, try using it first.
                    return v2Credentials;
                }

                return credentialProvider.GetCredentials(uri, proxy, CredentialType.ProxyCredentials, retrying: false);
            };

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.ProxyPassed = proxy =>
            {
                // add the proxy to v2 proxy cache.
                v2ProxyCache?.Add(proxy);
            };
            
            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForCredentials = uri =>
            {
                bool retrying = _credentialRequested.Contains(uri);

                // Add uri to the hash set so that the next time we know the credentials for this
                // uri has been requested before. In this case, NuGet will ask user for credentials.
                if (!retrying)
                {
                    _credentialRequested.Add(uri);
                }

                return credentialProvider.GetCredentials(
                    uri,
                    proxy: null,
                    credentialType: CredentialType.RequestCredentials,
                    retrying: retrying);
            };

            var v2CredentialStoreType = typeof(NuGet.ICredentialCache).Assembly.GetType("NuGet.CredentialStore");
            var property = v2CredentialStoreType?.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            var v2CredentialStore = property?.GetValue(obj: null) as NuGet.ICredentialCache;

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.CredentialsSuccessfullyUsed = (uri, credentials) =>
            {
                v2CredentialStore?.Add(uri, credentials);
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