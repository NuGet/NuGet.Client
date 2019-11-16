using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using static NuGet.Commands.ClientCertificatesCommandArgs;
using static NuGet.Configuration.CertificateSearchItem;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand),
        "client-certificates",
        "ClientCertificatesDescription",
        MinArgs = 0,
        MaxArgs = 2,
        UsageSummaryResourceName = "ClientCertificatesCommandUsageSummary",
        UsageExampleResourceName = "ClientCertificatesCommandUsageExamples")]
    public class ClientCertificatesCommand : Command
    {
        internal ClientCertificatesCommand()
        {
        }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandCheckCertificateDescription")] //TODO
        public bool CheckCertificate { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandFindTypeDescription")] //TODO
        public string FindType { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandFindValueDescription")] //TODO
        public string FindValue { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandNameDescription")] //TODO
        public string Name { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandPasswordDescription")] //TODO
        public string Password { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandPathDescription")] //TODO
        public string Path { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandPEMDescription")] //TODO
        public string PEM { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandSourceTypeDescription")] //TODO
        public string SourceType { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandStoreLocationDescription")] //TODO
        public string StoreLocation { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandStoreNameDescription")] //TODO
        public string StoreName { get; set; }

        [Option(typeof(NuGetCommand), "ClientCertificatesCommandStorePasswordInClearTextDescription")] //TODO
        public bool StorePasswordInClearText { get; set; }

        internal IClientCertificatesCommandRunner ClientCertificatesCommandRunner { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            var actionString = Arguments.FirstOrDefault();

            ClientCertificatesCommandAction action;
            if (string.IsNullOrEmpty(actionString))
            {
                action = ClientCertificatesCommandAction.List;
            }
            else if (!Enum.TryParse(actionString, true, out action))
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownAction, actionString));
            }

            ClientCertificatesSourceType? sourceType;
            if (string.IsNullOrEmpty(SourceType))
            {
                sourceType = GuessSourceTypeBasedOnArgument(action);
            }
            else if (Enum.TryParse(SourceType, true, out ClientCertificatesSourceType sourceTypeValue))
            {
                sourceType = sourceTypeValue;
            }
            else
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownClientCertificatesSourceType, SourceType));
            }

            StoreName? storeName;
            if (string.IsNullOrEmpty(StoreName))
            {
                storeName = null;
            }
            else if (Enum.TryParse(StoreName, true, out StoreName storeNameValue))
            {
                storeName = storeNameValue;
            }
            else
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownClientCertificatesStoreName, StoreName));
            }

            StoreLocation? storeLocation;
            if (string.IsNullOrEmpty(StoreLocation))
            {
                storeLocation = null;
            }
            else if (Enum.TryParse(StoreLocation, true, out StoreLocation storeLocationValue))
            {
                storeLocation = storeLocationValue;
            }
            else
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownClientCertificatesStoreLocation, StoreLocation));
            }

            X509FindType? findType;
            if (string.IsNullOrEmpty(FindType))
            {
                findType = null;
            }
            else if (Enum.TryParse(FindType, true, out X509FindType findTypeValue))
            {
                findType = findTypeValue;
            }
            else
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownClientCertificatesFindType, FindType));
            }

            var clientCertificateProvider = new ClientCertificateProvider(Settings);

            var args = new ClientCertificatesCommandArgs
            {
                Action = action,
                SourceType = sourceType,
                CheckCertificate = CheckCertificate,
                Name = Name,
                Path = Path,
                PEM = PEM,
                Password = Password,
                StorePasswordInClearText = StorePasswordInClearText,
                StoreLocation = storeLocation,
                StoreName = storeName,
                FindType = findType,
                FindValue = FindValue,
                Logger = Console
            };

            if (ClientCertificatesCommandRunner == null)
            {
                ClientCertificatesCommandRunner = new ClientCertificatesCommandRunner(clientCertificateProvider);
            }

            var result = await ClientCertificatesCommandRunner.ExecuteCommandAsync(args);

            if (result > 0)
            {
                throw new ExitCodeException(1);
            }
        }

        private ClientCertificatesSourceType? GuessSourceTypeBasedOnArgument(ClientCertificatesCommandAction action)
        {
            if (action != ClientCertificatesCommandAction.Add) return null;

            if (!string.IsNullOrWhiteSpace(Path))
            {
                return ClientCertificatesSourceType.File;
            }

            if (!string.IsNullOrWhiteSpace(PEM))
            {
                return ClientCertificatesSourceType.PEM;
            }

            if (!string.IsNullOrWhiteSpace(StoreLocation) || !string.IsNullOrWhiteSpace(StoreName) ||
                !string.IsNullOrWhiteSpace(FindType) || !string.IsNullOrWhiteSpace(FindValue))
            {
                return ClientCertificatesSourceType.Storage;
            }

            return null;
        }
    }
}
