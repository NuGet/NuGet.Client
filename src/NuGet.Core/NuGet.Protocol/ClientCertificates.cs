using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Protocol.Core.Types
{
    public static class ClientCertificates
    {
        #region Constructors

        static ClientCertificates()
        {
            //Default client certificates
            Certificates = new List<X509Certificate>();
        }

        #endregion

        #region Static members

        /// <summary>
        ///     Setup http client handler with stored client certificates
        /// </summary>
        /// <param name="httpClientHandler">Http client handler</param>
        public static void SetupClientHandler(HttpClientHandler httpClientHandler)
        {
            if (httpClientHandler == null)
            {
                throw new ArgumentNullException(nameof(httpClientHandler));
            }

            httpClientHandler.ClientCertificates.AddRange(Certificates.ToArray());
        }

        /// <summary>
        ///     Add client certificates which will be set to http clients
        /// </summary>
        /// <param name="certificate">Client certificate</param>
        public static void Add(X509Certificate certificate)
        {
            if (certificate == null) return;

            Certificates.Add(certificate);
        }

        private static readonly List<X509Certificate> Certificates;

        #endregion
    }
}
