using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Configuration;
using static NuGet.Configuration.CertificateSearchItem;

namespace NuGet.Commands
{
    public class ClientCertificatesCommandArgs
    {
        /// <summary>
        ///     Action to be performed by the client certificates command.
        /// </summary>
        public ClientCertificatesCommandAction Action { get; set; }

        /// <summary>
        ///     Indicates that certificate must be checked before Add or check certificate existence on List
        /// </summary>
        public bool CheckCertificate { get; set; }

        /// <summary>
        ///     FindType added to a storage client certificate source
        /// </summary>
        public X509FindType? FindType { get; set; }

        /// <summary>
        ///     FindValue added to a storage client certificate source
        /// </summary>
        public string FindValue { get; set; }

        /// <summary>
        ///     Logger to be used to display the logs during the execution of sign command.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        ///     Name of the client certificate item.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Password for the certificate, if needed. This option can be used to specify the password for the certificate.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     Path to certificate file added to a file client certificate source
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     Base64 encoded DER certificate in PEM format added to a PEM client certificate source
        /// </summary>
        public string PEM { get; set; }

        /// <summary>
        ///     Client certificate source type.
        /// </summary>
        public ClientCertificatesSourceType? SourceType { get; set; }

        /// <summary>
        ///     StoreLocation added to a storage client certificate source
        /// </summary>
        public StoreLocation? StoreLocation { get; set; }

        /// <summary>
        ///     StoreName added to a storage client certificate source
        /// </summary>
        public StoreName? StoreName { get; set; }

        /// <summary>
        ///     Enables storing password for the certificate by disabling password encryption.
        /// </summary>
        public bool StorePasswordInClearText { get; set; }

        public string GetPassword()
        {
            if (string.IsNullOrEmpty(Password)) return null;

            if (StorePasswordInClearText) return Password;
            return EncryptionUtility.EncryptString(Password);
        }

        public enum ClientCertificatesCommandAction
        {
            Add,
            List,
            Remove
        }
    }
}
