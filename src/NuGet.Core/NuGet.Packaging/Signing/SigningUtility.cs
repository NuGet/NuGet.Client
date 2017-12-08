// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
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

#if IS_DESKTOP
        /// <summary>
        /// Create an ordered list of certificates. The leaf node is returned first.
        /// </summary>
        public static IReadOnlyList<X509Certificate2> GetCertificateChain(
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (extraStore == null)
            {
                throw new ArgumentNullException(nameof(extraStore));
            }

            using (var chain = new X509Chain())
            {
                SetCertBuildChainPolicy(
                    chain,
                    extraStore,
                    DateTime.Now,
                    NuGetVerificationCertificateType.Signature);

                if (chain.Build(certificate))
                {
                    return GetCertificateChain(chain);
                }

                foreach (var chainStatus in chain.ChainStatus)
                {
                    if (chainStatus.Status != X509ChainStatusFlags.NoError)
                    {
                        throw new SignatureException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString()));
                    }
                }

                // Should be unreachable.
                throw new SignatureException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChainUnspecifiedReason));
            }
        }
#endif

        /// <summary>
        /// Create an ordered list of certificates. The leaf node is returned first.
        /// </summary>
        /// <remarks>This does not check validity or trust. It returns the chain as-is.</remarks>
        public static IReadOnlyList<X509Certificate2> GetCertificateChain(X509Chain certChain)
        {
            if (certChain == null)
            {
                throw new ArgumentNullException(nameof(certChain));
            }

            var certs = new List<X509Certificate2>();

            foreach (var item in certChain.ChainElements)
            {
                certs.Add(item.Certificate);
            }

            return certs;
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
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsageOid))
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
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsageOid))
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

#if IS_DESKTOP
        public static CryptographicAttributeObjectCollection GetSignAttributes(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chain)
        {
            var attributes = new CryptographicAttributeObjectCollection
            {
                new Pkcs9SigningTime()
            };

            if (request.SignatureType != SignatureType.Unknown)
            {
                // Add signature type if set.
                attributes.Add(AttributeUtility.GetCommitmentTypeIndication(request.SignatureType));
            }

            // Add the full chain of certificate hashes
            attributes.Add(AttributeUtility.GetSigningCertificateV2(chain, request.SignatureHashAlgorithm));

            return attributes;
        }

        internal static void SetCertBuildChainPolicy(
            X509Chain x509Chain,
            X509Certificate2Collection additionalCertificates,
            DateTime verificationTime,
            NuGetVerificationCertificateType certificateType)
        {
            var policy = x509Chain.ChainPolicy;

            if (certificateType == NuGetVerificationCertificateType.Signature)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.CodeSigningEkuOid));
            }
            else if (certificateType == NuGetVerificationCertificateType.Timestamp)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEkuOid));
                policy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
            }

            policy.ExtraStore.AddRange(additionalCertificates);

            policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            policy.RevocationMode = X509RevocationMode.Online;

            policy.VerificationTime = verificationTime;
        }
#endif
    }
}