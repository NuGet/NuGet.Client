using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using static NuGet.Commands.ClientCertificatesCommandArgs;

namespace NuGet.Commands
{
    /// <summary>
    ///     Command Runner used to run the business logic for nuget trusted-signers command
    /// </summary>
    public class ClientCertificatesCommandRunner : IClientCertificatesCommandRunner
    {
        private const int SuccessCode = 0;

        private readonly IClientCertificateProvider _clientCertificateProvider;

        public ClientCertificatesCommandRunner(IClientCertificateProvider clientCertificateProvider)
        {
            _clientCertificateProvider = clientCertificateProvider ?? throw new ArgumentNullException(nameof(clientCertificateProvider));
        }

        public async Task<int> ExecuteCommandAsync(ClientCertificatesCommandArgs args)
        {
            ILogger logger = args.Logger ?? NullLogger.Instance;

            switch (args.Action)
            {
                case ClientCertificatesCommandAction.List:
                    ValidateListArguments(args);

                    await ListAllItemsAsync(args, logger);

                    break;

                case ClientCertificatesCommandAction.Add:
                    ValidateAddArguments(args);

                    await AddItemAsync(args, logger);

                    break;

                case ClientCertificatesCommandAction.Remove:
                    ValidateRemoveArguments(args);

                    await RemoveItemAsync(args.Name, logger);

                    break;
            }

            return SuccessCode;
        }

        private async Task AddItemAsync(ClientCertificatesCommandArgs args, ILogger logger)
        {
            CertificateSearchItem item = _clientCertificateProvider.GetClientCertificates()
                                                                   .FirstOrDefault(i => string.Equals(i.Name, args.Name, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                if (item.SourceType != args.SourceType)
                {
                    throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                    Strings.Error_ExistingAndRequiredClientCertificateSourceMissmatch,
                                                                                    item.SourceType,
                                                                                    args.SourceType));
                }

                if (item is FromFileItem fromFileItem)
                {
                    if (args.Path != null) fromFileItem.Path = args.Path;
                    var password = args.GetPassword();
                    if (password != null) fromFileItem.Password = password;
                }

                if (item is FromPEMItem fromPEMItem)
                {
                    if (args.PEM != null) fromPEMItem.Base64Certificate = args.PEM;
                    var password = args.GetPassword();
                    if (password != null) fromPEMItem.Password = password;
                }

                if (item is FromStorageItem fromStorageItem)
                {
                    if (args.StoreLocation.HasValue) fromStorageItem.StoreLocation = args.StoreLocation.Value;
                    if (args.StoreName.HasValue) fromStorageItem.StoreName = args.StoreName.Value;
                    if (args.FindType.HasValue) fromStorageItem.FindType = args.FindType.Value;
                    if (args.FindValue != null) fromStorageItem.FindValue = args.FindValue;
                }
            }
            else
            {
                switch (args.SourceType)
                {
                    case CertificateSearchItem.ClientCertificatesSourceType.File:
                        item = new FromFileItem(args.Name, args.Path, args.GetPassword());
                        break;
                    case CertificateSearchItem.ClientCertificatesSourceType.PEM:
                        item = new FromPEMItem(args.Name, args.PEM, args.GetPassword());
                        break;
                    case CertificateSearchItem.ClientCertificatesSourceType.Storage:
                        item = new FromStorageItem(args.Name, args.FindValue, args.StoreLocation, args.StoreName, args.FindType);
                        break;
                    default:
                        throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                        Strings.Error_UnknownClientCertificateSourceType,
                                                                                        args.SourceType));
                }
            }

            if (args.CheckCertificate)
            {
                X509Certificate certificate = item.Search();
                if (certificate == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_ClientCertificateNotFound));
                }
            }

            _clientCertificateProvider.AddOrUpdate(item);

            await logger.LogAsync(LogLevel.Information, string.Format(CultureInfo.CurrentCulture, Strings.SuccessfullyAddedClientCertificate, args.Name));
        }

        private IReadOnlyList<CertificateSearchItem> FilterClientCertificates(IReadOnlyList<CertificateSearchItem> items, ClientCertificatesCommandArgs args)
        {
            if (args.SourceType.HasValue) items = items.Where(i => i.SourceType == args.SourceType.Value).ToList();

            IEnumerable<FromPEMItem> fromPEMItems = items.OfType<FromPEMItem>();
            if (!string.IsNullOrEmpty(args.PEM))
            {
                fromPEMItems = fromPEMItems.Where(i => (i.Base64Certificate ?? string.Empty).IndexOf(args.PEM, StringComparison.OrdinalIgnoreCase) > -1);
            }

            IEnumerable<FromFileItem> fromFileItems = items.OfType<FromFileItem>();
            if (!string.IsNullOrEmpty(args.Path))
            {
                fromFileItems = fromFileItems.Where(i => (i.Path ?? string.Empty).IndexOf(args.Path, StringComparison.OrdinalIgnoreCase) > -1);
            }

            IEnumerable<FromStorageItem> fromStorageItems = items.OfType<FromStorageItem>();
            if (args.StoreLocation.HasValue) fromStorageItems = fromStorageItems.Where(i => i.StoreLocation == args.StoreLocation.Value);
            if (args.StoreName.HasValue) fromStorageItems = fromStorageItems.Where(i => i.StoreName == args.StoreName.Value);
            if (args.FindType.HasValue) fromStorageItems = fromStorageItems.Where(i => i.FindType == args.FindType.Value);
            if (!string.IsNullOrEmpty(args.FindValue))
            {
                fromStorageItems = fromStorageItems.Where(i => (i.FindValue ?? string.Empty).IndexOf(args.FindValue, StringComparison.OrdinalIgnoreCase) > -1);
            }

            return Enumerable.Empty<CertificateSearchItem>()
                             .Union(fromPEMItems)
                             .Union(fromFileItems)
                             .Union(fromStorageItems)
                             .ToList();
        }

        private async Task ListAllItemsAsync(ClientCertificatesCommandArgs args, ILogger logger)
        {
            IReadOnlyList<CertificateSearchItem> items = _clientCertificateProvider.GetClientCertificates();

            items = FilterClientCertificates(items, args);

            if (!items.Any())
            {
                await logger.LogAsync(LogLevel.Information, Strings.NoClientCertificates);
                return;
            }

            var clientCertificatesLogs = new List<LogMessage>();

            await logger.LogAsync(LogLevel.Information, Strings.RegsiteredClientCertificates);
            await logger.LogAsync(LogLevel.Information, Environment.NewLine);

            for (var i = 0; i < items.Count; i++)
            {
                CertificateSearchItem item = items[i];

                var builder = new StringBuilder();

                var index = $" {i + 1}.".PadRight(6);
                var defaultIndentation = string.Empty.PadRight(6);

                builder.AppendLine($"{index}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesLogTitle, item.Name, item.ElementName)}");

                if (item is FromPEMItem fromPEMItem)
                {
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromPEM, fromPEMItem.Base64Certificate)}");
                    if (string.IsNullOrEmpty(fromPEMItem.Password))
                    {
                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromPEMOrFilePasswordNotSet)}");
                    }
                    else
                    {
                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromPEMOrFilePasswordSet)}");
                    }
                }

                if (item is FromFileItem fromFileItem)
                {
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromFilePath, fromFileItem.Path)}");
                    if (string.IsNullOrEmpty(fromFileItem.Password))
                    {
                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromPEMOrFilePasswordNotSet)}");
                    }
                    else
                    {
                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromPEMOrFilePasswordSet)}");
                    }
                }

                if (item is FromStorageItem fromStorageItem)
                {
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromStorageStoreLocation, fromStorageItem.StoreLocation)}");
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromStorageStoreName, fromStorageItem.StoreName)}");
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromStorageFindType, fromStorageItem.FindType)}");
                    builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesFromStorageFindValue, fromStorageItem.FindValue)}");
                }

                if (args.CheckCertificate)
                {
                    try
                    {
                        X509Certificate certificate = item.Search();
                        if (certificate == null)
                        {
                            throw new Exception(Strings.Error_ClientCertificateNotFound);
                        }

                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesItemCertificateMessage, certificate.GetCertHashString())}");
                    }
                    catch (Exception e)
                    {
                        builder.AppendLine($"{defaultIndentation}{string.Format(CultureInfo.CurrentCulture, Strings.ClientCertificatesItemCertificateMessage, e.GetBaseException().Message)}");
                    }
                }

                clientCertificatesLogs.Add(new LogMessage(LogLevel.Information, builder.ToString()));
            }

            await logger.LogMessagesAsync(clientCertificatesLogs);
        }

        private async Task RemoveItemAsync(string name, ILogger logger)
        {
            List<CertificateSearchItem> items = _clientCertificateProvider.GetClientCertificates()
                                                                          .Where(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                                                                          .ToList();
            if (!items.Any())
            {
                await logger.LogAsync(LogLevel.Information, string.Format(CultureInfo.CurrentCulture, Strings.NoClientCertificatesMatching, name));
                return;
            }

            _clientCertificateProvider.Remove(items.ToList());

            await logger.LogAsync(LogLevel.Information, string.Format(CultureInfo.CurrentCulture, Strings.SuccessfullyRemovedClientCertificate, name));
        }

        private void ValidateAddArguments(ClientCertificatesCommandArgs args)
        {
            if (!args.SourceType.HasValue)
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_CouldNotAddClientCertificate, Strings.Error_InvalidCombinationOfArguments));
            }

            ValidateNameExists(args.Name);
        }

        private void ValidateListArguments(ClientCertificatesCommandArgs args)
        {
            var isPasswordProvided = !string.IsNullOrEmpty(args.Password);

            if (isPasswordProvided)
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_CouldNotListClientCertificates, Strings.Error_InvalidCombinationOfArguments));
            }
        }

        private void ValidateNameExists(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PropertyCannotBeNullOrEmpty, nameof(name)));
            }
        }

        private void ValidateRemoveArguments(ClientCertificatesCommandArgs args)
        {
            ValidateNameExists(args.Name);

            var isSourceTypeProvided = args.SourceType.HasValue;
            var isPasswordProvided = !string.IsNullOrEmpty(args.Password);
            var isPathProvided = !string.IsNullOrEmpty(args.Path);
            var isPEMProvided = !string.IsNullOrEmpty(args.PEM);

            var isStoreLocationProvided = args.StoreLocation.HasValue;
            var isStoreNameProvided = args.StoreName.HasValue;
            var isFindTypeProvided = args.FindType.HasValue;
            var isFindValueProvided = !string.IsNullOrEmpty(args.FindValue);

            if (isSourceTypeProvided || isPasswordProvided ||
                isPathProvided || isPEMProvided || isStoreLocationProvided ||
                isStoreNameProvided || isFindTypeProvided || isFindValueProvided)
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                Strings.Error_CouldNotRemoveClientCertificate,
                                                                                Strings.Error_InvalidCombinationOfArguments));
            }
        }
    }
}
