// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NuGet.Commands
{
    public static class CertificateUtility
    {
        private const int _limit = 10;

        public static string X509Certificate2ToString(X509Certificate2 cert)
        {          
            var certStringBuilder = new StringBuilder();

            certStringBuilder.AppendLine(string.Format(Strings.SignCommandCertificateSubjectName, cert.Subject));
            certStringBuilder.AppendLine(string.Format(Strings.SignCommandCertificateHash, cert.Thumbprint));
            certStringBuilder.AppendLine(string.Format(Strings.SignCommandCertificateIssuer, cert.IssuerName.Name));
            certStringBuilder.AppendLine(string.Format(Strings.SignCommandCertificateValidity, cert.NotBefore, cert.NotAfter));

            return certStringBuilder.ToString();
        }

        public static string X509Certificate2CollectionToString(X509Certificate2Collection certCollection)
        {
            var collectionStringBuilder = new StringBuilder();

            collectionStringBuilder.AppendLine(Strings.SignCommandMultipleCertificatesHeader);

            for (var i=0; i<Math.Min(_limit, certCollection.Count); i++)
            {
                var cert = certCollection[i];
                collectionStringBuilder.AppendLine(X509Certificate2ToString(cert));
            }

            if (certCollection.Count > _limit)
            {
                collectionStringBuilder.AppendLine(string.Format(Strings.SignCommandMultipleCertificatesFooter, certCollection.Count - _limit));
            }

            return collectionStringBuilder.ToString();
        }
    }
}
