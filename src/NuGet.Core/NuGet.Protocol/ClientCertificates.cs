using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Protocol.Core.Types
{
    public static class ClientCertificates
    {
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

            httpClientHandler.ClientCertificates.AddRange(Certificates);
        }

        /// <summary>
        ///     Store client certificates which will be set to http clients
        /// </summary>
        /// <param name="certificates"></param>
        public static void Store(IEnumerable<X509Certificate> certificates)
        {
            if (certificates == null)
            {
                Certificates = Enumerable.Empty<X509Certificate>().ToArray();
            }
            else
            {
                Certificates = certificates.Where(c => c != null).ToArray();
            }
        }

        private static X509Certificate[] Certificates { get; set; }

        #endregion

        #region Constructors

        static ClientCertificates()
        {
            //Default client certificates
            Certificates = Enumerable.Empty<X509Certificate>().ToArray();
        }

        #endregion
    }
}
