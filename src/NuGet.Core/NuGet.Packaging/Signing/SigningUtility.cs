// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Utility methods for signing.
    /// </summary>
    public static class SigningUtility
    {
        // Oid for certificate extension: "extKeyUsage" (Extended key usage) 
        private const string _ekuOid = "2.5.29.37";

        /// <summary>
        /// Checks the validity of an X509Certificate2 is valid by building it's X509Chain.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="chain">X509Chain built from the X509Certificate2 if the return value is true.</param>
        /// <param name="allowUntrustedRoot">Sets X509VerificationFlags.AllowUnknownCertificateAuthority if set to true.</param>
        /// <returns>A bool indicating if the certificate builds a valid X509Chain.</returns>
        public static bool IsCertificateValid(X509Certificate2 certificate, out X509Chain chain, bool allowUntrustedRoot)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            chain = new X509Chain();

            if (allowUntrustedRoot)
            {
                chain.ChainPolicy.VerificationFlags |= X509VerificationFlags.AllowUnknownCertificateAuthority;
            }

            return chain.Build(certificate);
        }

        /// <summary>
        /// Checks the validity of an X509Certificate2 is valid by building it's X509Chain.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="chain">X509Chain built from the X509Certificate2 if the return value is true.</param>
        /// <param name="allowUntrustedRoot">Sets X509VerificationFlags.AllowUnknownCertificateAuthority if set to true.</param>
        /// <param name="checkRevocationStatus">Sets X509Chain.ChainPolicy.RevocationMode to allow online revocation checks.</param>
        /// <returns>A bool indicating if the certificate builds a valid X509Chain.</returns>
        public static bool IsCertificateValid(X509Certificate2 certificate, out X509Chain chain, bool allowUntrustedRoot, bool checkRevocationStatus)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            chain = new X509Chain();

            if (checkRevocationStatus)
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            }

            if (allowUntrustedRoot)
            {
                chain.ChainPolicy.VerificationFlags |= X509VerificationFlags.AllowUnknownCertificateAuthority;
            }

            return chain.Build(certificate);
        }

        /// <summary>
        /// Checks if an X509Certificate2 contains a Enhanced Key Usage specified in the form of an Oid.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="oid">String Oid of the Enhanced Key Usage</param>
        /// <returns>A bool indicating if the X509Certificate2 contains specified Oid string in it's Enhanced Key Usage.</returns>
        public static bool CertificateContainsEku(X509Certificate2 certificate, string oid)
        {
            var certEkuOidCollection = GetCertificateEKU(certificate);

            foreach(var ekuOid in certEkuOidCollection)
            {
                if (string.Equals(ekuOid.Value, oid))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the Enhanced Key Usage of an X509Certificate2.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <returns>An OidCollection object contained all the Enhanced Key Usage fileds in Oid form.</returns>
        private static OidCollection GetCertificateEKU(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var oidCollection = new OidCollection();

            foreach (var ext in certificate.Extensions)
            {
                if (string.Equals(ext.Oid.Value, _ekuOid))
                {
                    var eku = (X509EnhancedKeyUsageExtension)ext;
                    oidCollection = eku.EnhancedKeyUsages;
                }
            }

            return oidCollection;
        }
    }
}
