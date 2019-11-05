using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Configuration
{
    public static class ClientCertificateProvider
    {
        public static IEnumerable<X509Certificate> Provide(ISettings settings)
        {
            SettingSection clientCertificatesSection = settings.GetSection(ConfigurationConstants.ClientCertificates);
            return clientCertificatesSection?.Items
                                            .OfType<CertificateSearchItem>()
                                            .Select(i => i.Search());
        }
    }
}
