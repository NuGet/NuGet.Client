// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NuGet.Common;

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
            X509Certificate2ToString(cert, certStringBuilder, indentation: "");
            return certStringBuilder.ToString();
        }

        private static void X509Certificate2ToString(X509Certificate2 cert, StringBuilder certStringBuilder, string indentation)
        {
            certStringBuilder.AppendLine($"{indentation}{string.Format(CultureInfo.CurrentCulture, Strings.CertUtilityCertificateSubjectName, cert.Subject)}");
            certStringBuilder.AppendLine($"{indentation}{string.Format(CultureInfo.CurrentCulture, Strings.CertUtilityCertificateHash, cert.Thumbprint)}");
            certStringBuilder.AppendLine($"{indentation}{string.Format(CultureInfo.CurrentCulture, Strings.CertUtilityCertificateIssuer, cert.IssuerName.Name)}");
            certStringBuilder.AppendLine($"{indentation}{string.Format(CultureInfo.CurrentCulture, Strings.CertUtilityCertificateValidity, cert.NotBefore, cert.NotAfter)}");
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
                X509Certificate2ToString(cert, collectionStringBuilder, indentation: "");
                collectionStringBuilder.AppendLine();
            }

            if (certCollection.Count > _limit)
            {
                collectionStringBuilder.AppendLine(string.Format(Strings.CertUtilityMultipleCertificatesFooter, certCollection.Count - _limit));
            }

            return collectionStringBuilder.ToString();
        }


        public static string X509ChainToString(X509Chain chain)
        {
            var collectionStringBuilder = new StringBuilder();
            var indentationLevel = "    ";
            var indentation = indentationLevel;

            var chainElementsCount = chain.ChainElements.Count;
            // Start in 1 to omit main certificate (only build the chain)
            for (var i = 1; i < Math.Min(_limit, chainElementsCount); i++)
            {
                X509Certificate2ToString(chain.ChainElements[i].Certificate, collectionStringBuilder, indentation);
                collectionStringBuilder.AppendLine();
                indentation += indentationLevel;
            }

            if (chainElementsCount > _limit)
            {
                collectionStringBuilder.AppendLine(string.Format(Strings.CertUtilityMultipleCertificatesFooter, chainElementsCount - _limit));
            }

            return collectionStringBuilder.ToString();
        }

        /// <summary>
        /// Determines if a certificate's signature algorithm is supported.
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if the certificate's signature algorithm is supported.</returns>
        public static bool IsSignatureAlgorithmSupported(X509Certificate2 certificate)
        {
            switch (certificate.SignatureAlgorithm.Value)
            {
                case Oids.Sha256WithRSAEncryption:
                case Oids.Sha384WithRSAEncryption:
                case Oids.Sha512WithRSAEncryption:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary> 
        /// Validates the public key requirements for a certificate 
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
        /// Validates if the certificate contains the lifetime signing EKU
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if the certificate has the lifetime signing EKU</returns>
        public static bool HasLifetimeSigningEku(X509Certificate2 certificate)
        {
            return HasExtendedKeyUsage(certificate, Oids.LifetimeSignerEku);
        }

        /// <summary>
        /// Checks if an X509Certificate2 contains a particular Extended Key Usage (EKU).
        /// </summary>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="ekuOid">String OID of the Extended Key Usage</param>
        /// <returns>A bool indicating if the X509Certificate2 contains specified OID in its Extended Key Usage.</returns>
        public static bool HasExtendedKeyUsage(X509Certificate2 certificate, string ekuOid)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsage))
                {
                    var ekuExtension = (X509EnhancedKeyUsageExtension)extension;

                    foreach (var eku in ekuExtension.EnhancedKeyUsages)
                    {
                        if (eku.Value == ekuOid)
                        {
                            return true;
                        }
                    }

                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an X509Certificate2 is valid for a particular purpose.
        /// </summary>
        /// <remarks>
        /// This must not be used in evaluation of a signed package.
        /// A more accurate test is building a chain with the specified EKU asserted in the application policy.
        /// </remarks>
        /// <param name="certificate">X509Certificate2 to be checked.</param>
        /// <param name="ekuOid">String OID of the Extended Key Usage</param>
        /// <returns>A bool indicating if the X509Certificate2 contains specified OID string in its Extended Key Usage.</returns>
        public static bool IsValidForPurposeFast(X509Certificate2 certificate, string ekuOid)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsage))
                {
                    var ekuExtension = (X509EnhancedKeyUsageExtension)extension;

                    if (ekuExtension.EnhancedKeyUsages.Count == 0)
                    {
                        return true;
                    }

                    foreach (var eku in ekuExtension.EnhancedKeyUsages)
                    {
                        if (eku.Value == ekuOid)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return true;
        }

        public static bool IsCertificateValidityPeriodInTheFuture(X509Certificate2 certificate)
        {
            return DateTime.Now < certificate.NotBefore;
        }

        internal static byte[] GetHash(X509Certificate2 certificate, HashAlgorithmName hashAlgorithm)
        {
            return hashAlgorithm.ComputeHash(certificate.RawData);
        }
    }
}