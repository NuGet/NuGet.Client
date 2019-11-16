using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface IClientCertificateProvider
    {
        /// <summary>
        ///     Adds a new client certificate or updates an existing one in the settings.
        /// </summary>
        /// <param name="item">Client certificate to be added or updated</param>
        void AddOrUpdate(CertificateSearchItem item);

        /// <summary>
        ///     Get a list of all the trusted signer entries under the computer trusted signers section.
        /// </summary>
        IReadOnlyList<CertificateSearchItem> GetClientCertificates();

        /// <summary>
        ///     Removes client certificates from the settings.
        /// </summary>
        /// <param name="items">Client certificates to be removed</param>
        void Remove(IReadOnlyList<CertificateSearchItem> items);
    }
}