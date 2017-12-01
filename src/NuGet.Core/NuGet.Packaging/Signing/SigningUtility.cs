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
        /// <summary>
        /// Validates the public key requiriements for a certificate
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if the certificate's public key is valid within NuGet signature requirements</returns>
        public static bool IsCertificatePublicKeyValid(X509Certificate2 certificate)
        {
            // Check if the public key is RSA with a valid keysize
            var RSAPublicKey = RSACertificateExtensions.GetRSAPublicKey(certificate);
            if (RSAPublicKey != null)
            {
                return RSAPublicKey.KeySize >= SigningSpecifications.V1.RSAPublicKeyMinLength;
            }

            return false;
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
                if (string.Equals(ext.Oid.Value, Oids.EnhancedKeyUsageOid))
                {
                    var eku = (X509EnhancedKeyUsageExtension)ext;
                    oidCollection = eku.EnhancedKeyUsages;
                }
            }

            return oidCollection;
        }
    }
}
