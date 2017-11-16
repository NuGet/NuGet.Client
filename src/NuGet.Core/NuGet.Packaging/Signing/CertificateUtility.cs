// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NuGet.Packaging.Signing
{
    public static class CertificateUtility
    {
        private const int _limit = 10;

        /// <summary>
        /// Converts a X509Certificate2 to a human friendly string of the following format -
        /// Subject Name: CN=name
        /// SHA1 hash: hash
        /// Issued by: CN=issuer
        /// Valid from: issue date time to expiry date time in local time
        /// </summary>
        /// <param name="cert">X509Certificate2 to be converted to string.</param>
        /// <returns>string representation of the X509Certificate2.</returns>
        public static string X509Certificate2ToString(X509Certificate2 cert)
        {
            var certStringBuilder = new StringBuilder();
            X509Certificate2ToString(cert, certStringBuilder);
            return certStringBuilder.ToString();
        }

        private static void X509Certificate2ToString(X509Certificate2 cert, StringBuilder certStringBuilder)
        {
            certStringBuilder.AppendLine(string.Format(Strings.CertUtilityCertificateSubjectName, cert.Subject));
            certStringBuilder.AppendLine(string.Format(Strings.CertUtilityCertificateHash, cert.Thumbprint));
            certStringBuilder.AppendLine(string.Format(Strings.CertUtilityCertificateIssuer, cert.IssuerName.Name));
            certStringBuilder.AppendLine(string.Format(Strings.CertUtilityCertificateValidity, cert.NotBefore, cert.NotAfter));
        }

        /// <summary>
        /// Converts a X509Certificate2Collection to a human friendly string of the following format -
        /// Subject Name: CN=name
        /// SHA1 hash: hash
        /// Issued by: CN=issuer
        /// Valid from: issue date time to expiry date time in local time
        ///
        /// Subject Name: CN=name
        /// SHA1 hash: hash
        /// Issued by: CN=issuer
        /// Valid from: issue date time to expiry date time in local time
        ///
        /// ... N more.
        /// </summary>
        /// <param name="certCollection">X509Certificate2Collection to be converted to string.</param>
        /// <returns>string representation of the X509Certificate2Collection.</returns>
        public static string X509Certificate2CollectionToString(X509Certificate2Collection certCollection)
        {
            var collectionStringBuilder = new StringBuilder();

            collectionStringBuilder.AppendLine(Strings.CertUtilityMultipleCertificatesHeader);

            for (var i = 0; i < Math.Min(_limit, certCollection.Count); i++)
            {
                var cert = certCollection[i];
                X509Certificate2ToString(cert, collectionStringBuilder);
                collectionStringBuilder.AppendLine();
            }

            if (certCollection.Count > _limit)
            {
                collectionStringBuilder.AppendLine(string.Format(Strings.CertUtilityMultipleCertificatesFooter, certCollection.Count - _limit));
            }

            return collectionStringBuilder.ToString();
        }
    }
}
