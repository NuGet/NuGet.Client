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
        /// Checks the validity of an X509Certificate2 is valid by building it's X509Chain.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="chain">X509Chain built from the X509Certificate2 if the return value is true.</param>
        /// <param name="allowUntrustedRoot">Sets X509VerificationFlags.AllowUnknownCertificateAuthority if set to true.</param>
        /// <param name="checkRevocationMode">X509Chain.ChainPolicy.RevocationMode to allow revocation checks.</param>
        /// <returns>A bool indicating if the certificate builds a valid X509Chain.</returns>
        public static bool IsCertificateValid(X509Certificate2 certificate, X509Certificate2Collection additionalCertificates, out X509Chain chain, bool allowUntrustedRoot, X509RevocationMode checkRevocationMode)
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

            chain.ChainPolicy.RevocationMode = checkRevocationMode;
            if (checkRevocationMode != X509RevocationMode.NoCheck)
            {
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            }

            var extraStore = chain.ChainPolicy.ExtraStore;
            extraStore.AddRange(additionalCertificates);

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
