// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Utility methods for signing.
    /// </summary>
    public static class SigningUtility
    {

        /// <summary>
        /// Checks the validity of an X509Certificate2 is valid by building it's X509Chain.
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="chain">X509Chain built from the X509Certificate2 if the return value is true.</param>
        /// <param name="allowUntrustedRoot">Sets X509VerificationFlags.AllowUnknownCertificateAuthority if set to true.</param>
        /// <returns>A bool indicating if the certificate builds a valid X509Chain.</returns>
        public static bool IsCertificateChainValid(X509Certificate2 certificate, X509Certificate2Collection additionalCertificates, bool allowUntrustedRoot)
        {
            return IsCertificateValid(certificate, out var chain, allowUntrustedRoot, checkRevocationStatus: true);
        }

        public static bool IsCertificatePublicKeyValid(X509Certificate2 certificate)
        {
            // Check if the public key is RSA with a valid keysize
            var RSAPublicKey = RSACertificateExtensions.GetRSAPublicKey(certificate);
            if (RSAPublicKey != null)
            {
                return RSAPublicKey.KeySize >= SigningSpecifications.V1.RSAPublicKeyMinLength;
            }

            // Check if the certificate uses a valid ECDsa public key
            return SigningSpecifications.V1.IsECDsaPublicKeyCurveValid(certificate.PublicKey.EncodedParameters.RawData);
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
            // TODO add support for additional certificates
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

#if IS_DESKTOP
        public static CryptographicAttributeObjectCollection GetSignAttributes()
        {
            var attributes = new CryptographicAttributeObjectCollection();

            attributes.Add(new Pkcs9SigningTime());

            return attributes;
        }
#endif

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
