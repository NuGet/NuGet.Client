using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    public class ClientCertificateProvider : IClientCertificateProvider
    {
        private readonly ISettings _settings;

        public ClientCertificateProvider(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void AddOrUpdate(CertificateSearchItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Remove(new[] { item });

            _settings.AddOrUpdate(ConfigurationConstants.ClientCertificates, item);

            _settings.SaveToDisk();
        }

        public void Remove(IReadOnlyList<CertificateSearchItem> items)
        {
            if (items == null || !items.Any())
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(items));
            }

            foreach (CertificateSearchItem signer in items)
            {
                try
                {
                    _settings.Remove(ConfigurationConstants.ClientCertificates, signer);
                }
                // An error means the item doesn't exist or is in a machine wide config, therefore just ignore it
                catch
                {
                }
            }

            _settings.SaveToDisk();
        }

        public IReadOnlyList<CertificateSearchItem> GetClientCertificates()
        {
            SettingSection clientCertificatesSection = _settings.GetSection(ConfigurationConstants.ClientCertificates);

            List<CertificateSearchItem> result = clientCertificatesSection?.Items
                                                                          .OfType<CertificateSearchItem>()
                                                                          .ToList();
            return result ?? Enumerable.Empty<CertificateSearchItem>().ToList();
        }
    }
}
