// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public static class SignatureUtility
    {
#if IS_DESKTOP
        /// <summary>
        /// Gets certificates in the certificate chain for the primary signature.
        /// </summary>
        /// <param name="signature">The primary signature.</param>
        /// <returns>A read-only list of X.509 certificates ordered from signing certificate to root.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="signature" /> is <c>null</c>.</exception>
        /// <remarks>
        /// WARNING:  This method does not perform revocation, trust, or certificate validity checking.
        /// </remarks>
        public static IReadOnlyList<X509Certificate2> GetPrimarySignatureCertificates(Signature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return GetPrimarySignatureCertificates(
                signature.SignedCms,
                signature.SignerInfo,
                SigningSpecifications.V1,
                includeChain: true);
        }

        internal static IReadOnlyList<X509Certificate2> GetPrimarySignatureSigningCertificate(
            SignedCms signedCms,
            SignerInfo signerInfo,
            SigningSpecifications signingSpecifications)
        {
            return GetPrimarySignatureCertificates(signedCms, signerInfo, signingSpecifications, includeChain: false);
        }

        private static IReadOnlyList<X509Certificate2> GetPrimarySignatureCertificates(
            SignedCms signedCms,
            SignerInfo signerInfo,
            SigningSpecifications signingSpecifications,
            bool includeChain)
        {
            if (signedCms == null)
            {
                throw new ArgumentNullException(nameof(signedCms));
            }

            if (signerInfo == null)
            {
                throw new ArgumentNullException(nameof(signerInfo));
            }

            var errors = new Errors(
                noCertificate: NuGetLogCode.NU3010,
                noCertificateString: Strings.ErrorNoCertificate,
                invalidSignature: NuGetLogCode.NU3011,
                invalidSignatureString: Strings.InvalidPrimarySignature,
                chainBuildingFailed: NuGetLogCode.NU3018);

            return GetCertificates(signedCms, signerInfo, errors, signingSpecifications, includeChain);
        }

        /// <summary>
        /// Gets certificates in the certificate chain for a timestamp on the primary signature.
        /// </summary>
        /// <param name="signature">The primary signature.</param>
        /// <returns>A read-only list of X.509 certificates ordered from signing certificate to root.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="signature" /> is <c>null</c>.</exception>
        /// <remarks>
        /// WARNING:  This method does not perform revocation, trust, or certificate validity checking.
        /// </remarks>
        public static IReadOnlyList<X509Certificate2> GetPrimarySignatureTimestampSignatureCertificates(
            Signature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            var timestamp = signature.Timestamps.FirstOrDefault();

            if (timestamp == null)
            {
                throw new SignatureException(NuGetLogCode.NU3029, Strings.InvalidTimestampSignature);
            }

            return GetTimestampSignatureSigningCertificates(
                timestamp.SignedCms,
                SigningSpecifications.V1,
                includeChain: true);
        }

        internal static IReadOnlyList<X509Certificate2> GetTimestampSignatureSigningCertificate(
            SignedCms signedCms,
            SigningSpecifications signingSpecifications)
        {
            return GetTimestampSignatureSigningCertificates(
              signedCms,
              signingSpecifications,
              includeChain: false);
        }

        private static IReadOnlyList<X509Certificate2> GetTimestampSignatureSigningCertificates(
            SignedCms signedCms,
            SigningSpecifications signingSpecificates,
            bool includeChain)
        {
            var errors = new Errors(
                noCertificate: NuGetLogCode.NU3020,
                noCertificateString: Strings.TimestampNoCertificate,
                invalidSignature: NuGetLogCode.NU3021,
                invalidSignatureString: Strings.TimestampInvalid,
                chainBuildingFailed: NuGetLogCode.NU3028);

            if (signedCms.SignerInfos.Count != 1)
            {
                throw new SignatureException(errors.InvalidSignature, errors.InvalidSignatureString);
            }

            return GetCertificates(
                signedCms,
                signedCms.SignerInfos[0],
                errors,
                SigningSpecifications.V1,
                includeChain);
        }

        private static IReadOnlyList<X509Certificate2> GetCertificates(
            SignedCms signedCms,
            SignerInfo signerInfo,
            Errors errors,
            SigningSpecifications signingSpecifications,
            bool includeChain)
        {
            if (signedCms == null)
            {
                throw new ArgumentNullException(nameof(signedCms));
            }

            if (signingSpecifications == null)
            {
                throw new ArgumentNullException(nameof(signingSpecifications));
            }

            if (signerInfo.Certificate == null)
            {
                throw new SignatureException(errors.NoCertificate, errors.NoCertificateString);
            }

            /*
                The signing-certificate and signing-certificate-v2 attributes are described in RFC 2634 and RFC 5035.

                Timestamps
                --------------------------------------------------
                RFC 3161 requires the signing-certificate attribute.  The RFC 5816 update introduces the newer
                signing-certificate-v2 attribute, which may replace or, for backwards compatibility, be present
                alongside the older ssigning-certificate attribute.

                The issuerSerial field is not required, but should be validated if present.


                Author and repository signatures
                --------------------------------------------------
                RFC 5126 (CAdES) requires that exactly one of either of these attributes be present, and also requires that
                the issuerSerial field be present.


                Validation
                --------------------------------------------------
                For author and repository signatures:

                    * the signing-certificate attribute must not be present
                    * the signing-certificate-v2 attribute be present
                    * the issuerSerial field must be present


                References:

                    "Signing Certificate Attribute Definition", RFC 2634 section 5.4 (https://tools.ietf.org/html/rfc2634#section-5.4)
                    "Certificate Identification", RFC 2634 section 5.4 (https://tools.ietf.org/html/rfc2634#section-5.4.1)
                    "Enhanced Security Services (ESS) Update: Adding CertID Algorithm Agility", RFC 5035 (https://tools.ietf.org/html/rfc5035)
                    "Request Format", RFC 3161 section 2.4.1 (https://tools.ietf.org/html/rfc3161#section-2.4.1)
                    "Signature of Time-Stamp Token", RFC 5816 section 2.2.1 (https://tools.ietf.org/html/rfc5816#section-2.2.1)
                    "Signing Certificate Reference Attributes", RFC 5126 section 5.7.3 (https://tools.ietf.org/html/rfc5126.html#section-5.7.3)
                    "ESS signing-certificate Attribute Definition", RFC 5126 section 5.7.3.1 (https://tools.ietf.org/html/rfc5126.html#section-5.7.3.1)
                    "ESS signing-certificate-v2 Attribute Definition", RFC 5126 section 5.7.3.2 (https://tools.ietf.org/html/rfc5126.html#section-5.7.3.2)
            */

            CryptographicAttributeObject signingCertificateAttribute = null;
            CryptographicAttributeObject signingCertificateV2Attribute = null;

            foreach (var attribute in signerInfo.SignedAttributes)
            {
                switch (attribute.Oid.Value)
                {
                    case Oids.SigningCertificate:
                        if (signingCertificateAttribute != null)
                        {
                            throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateMultipleAttributes);
                        }

                        if (attribute.Values.Count != 1)
                        {
                            throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateMultipleAttributeValues);
                        }

                        signingCertificateAttribute = attribute;
                        break;

                    case Oids.SigningCertificateV2:
                        if (signingCertificateV2Attribute != null)
                        {
                            throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2MultipleAttributes);
                        }

                        if (attribute.Values.Count != 1)
                        {
                            throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2MultipleAttributeValues);
                        }

                        signingCertificateV2Attribute = attribute;
                        break;
                }
            }

            var signatureType = AttributeUtility.GetCommitmentTypeIndication(signerInfo);
            var isIssuerSerialRequired = false;

            switch (signatureType)
            {
                case SignatureType.Author:
                case SignatureType.Repository:
                    if (signingCertificateAttribute != null)
                    {
                        throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateAttributeMustNotBePresent);
                    }

                    if (signingCertificateV2Attribute == null)
                    {
                        throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2AttributeMustBePresent);
                    }

                    isIssuerSerialRequired = true;
                    break;
            }

            // This returns a new X509Certificate2Collection, which is mutable.
            // Changes to this collection instance are not reflected back to SignedCms.Certificates.
            var extraStore = signedCms.Certificates;

            if (signingCertificateV2Attribute != null)
            {
                var reader = signingCertificateV2Attribute.ToDerSequenceReader();
                var signingCertificateV2 = SigningCertificateV2.Read(reader);

                return GetCertificates(
                    signerInfo.Certificate,
                    extraStore,
                    signingCertificateV2,
                    errors,
                    includeChain,
                    signingSpecifications,
                    isIssuerSerialRequired);
            }
            else if (signingCertificateAttribute != null)
            {
                var reader = signingCertificateAttribute.ToDerSequenceReader();
                var signingCertificate = SigningCertificate.Read(reader);

                return GetCertificates(
                    signerInfo.Certificate,
                    extraStore,
                    signingCertificate,
                    errors,
                    includeChain);
            }

            return GetCertificateChain(signerInfo.Certificate, extraStore, includeChain);
        }

        private static IReadOnlyList<X509Certificate2> GetCertificates(
            X509Certificate2 signerCertificate,
            X509Certificate2Collection extraStore,
            SigningCertificateV2 signingCertificateV2,
            Errors errors,
            bool includeChain,
            SigningSpecifications signingSpecifications,
            bool isIssuerSerialRequired)
        {
            if (signingCertificateV2.Certificates.Count == 0)
            {
                throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2Invalid);
            }

            if (!IsMatch(signerCertificate, signingCertificateV2.Certificates[0], errors, isIssuerSerialRequired))
            {
                throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2CertificateNotFound);
            }

            var chain = GetCertificateChain(signerCertificate, extraStore, includeChain);

            if (chain == null || chain.Count == 0)
            {
                throw new SignatureException(errors.ChainBuildingFailed, Strings.CertificateChainBuildFailed);
            }

            foreach (var essCertIdV2 in signingCertificateV2.Certificates)
            {
                // Verify hash algorithm is allowed
                if (!signingSpecifications.AllowedHashAlgorithmOids.Contains(
                    essCertIdV2.HashAlgorithm.Algorithm.Value,
                    StringComparer.Ordinal))
                {
                    throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateV2UnsupportedHashAlgorithm);
                }
            }

            return chain;
        }

        private static IReadOnlyList<X509Certificate2> GetCertificates(
            X509Certificate2 signerCertificate,
            X509Certificate2Collection extraStore,
            SigningCertificate signingCertificate,
            Errors errors,
            bool includeChain)
        {
            if (signingCertificate.Certificates.Count == 0)
            {
                throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateInvalid);
            }

            if (!IsMatch(signerCertificate, signingCertificate.Certificates[0]))
            {
                throw new SignatureException(errors.InvalidSignature, Strings.SigningCertificateCertificateNotFound);
            }

            var chain = GetCertificateChain(signerCertificate, extraStore, includeChain);

            if (chain == null || chain.Count == 0)
            {
                throw new SignatureException(errors.ChainBuildingFailed, Strings.CertificateChainBuildFailed);
            }

            return chain;
        }

        private static bool IsMatch(
            X509Certificate2 certificate,
            EssCertIdV2 essCertIdV2,
            Errors errors,
            bool isIssuerSerialRequired)
        {
            if (isIssuerSerialRequired)
            {
                if (essCertIdV2.IssuerSerial == null ||
                    essCertIdV2.IssuerSerial.GeneralNames.Count == 0)
                {
                    throw new SignatureException(errors.InvalidSignature, errors.InvalidSignatureString);
                }
            }

            if (essCertIdV2.IssuerSerial != null)
            {
                if (!AreSerialNumbersEqual(essCertIdV2.IssuerSerial, certificate))
                {
                    return false;
                }

                var generalName = essCertIdV2.IssuerSerial.GeneralNames.FirstOrDefault();

                if (generalName != null &&
                    generalName.DirectoryName != null &&
                    generalName.DirectoryName.Name != certificate.IssuerName.Name)
                {
                    return false;
                }
            }

            var hashAlgorithmName = CryptoHashUtility.OidToHashAlgorithmName(essCertIdV2.HashAlgorithm.Algorithm.Value);
            var actualHash = CertificateUtility.GetHash(certificate, hashAlgorithmName);

            return essCertIdV2.CertificateHash.SequenceEqual(actualHash);
        }

        private static bool IsMatch(X509Certificate2 certificate, EssCertId essCertId)
        {
            if (essCertId.IssuerSerial != null)
            {
                if (!AreSerialNumbersEqual(essCertId.IssuerSerial, certificate))
                {
                    return false;
                }

                var generalName = essCertId.IssuerSerial.GeneralNames.FirstOrDefault();

                if (generalName != null &&
                    generalName.DirectoryName != null &&
                    generalName.DirectoryName.Name != certificate.IssuerName.Name)
                {
                    return false;
                }
            }

            byte[] actualHash;

            using (var hashAlgorithm = CryptoHashUtility.GetSha1HashProvider())
            {
                actualHash = hashAlgorithm.ComputeHash(certificate.RawData);
            }

            return essCertId.CertificateHash.SequenceEqual(actualHash);
        }

        private static bool AreSerialNumbersEqual(IssuerSerial issuerSerial, X509Certificate2 certificate)
        {
            var certificateSerialNumber = certificate.GetSerialNumber();

            return issuerSerial.SerialNumber.SequenceEqual(certificateSerialNumber);
        }

        private static IReadOnlyList<X509Certificate2> GetCertificateChain(
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore,
            bool includeCertificatesAfterSigningCertificate)
        {
            if (!includeCertificatesAfterSigningCertificate)
            {
                return new[] { certificate };
            }

            using (var chain = new X509Chain())
            {
                chain.ChainPolicy.ExtraStore.AddRange(extraStore);

                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                chain.Build(certificate);

                if (chain.ChainStatus.Length > 0 &&
                    (chain.ChainStatus[0].Status & X509ChainStatusFlags.PartialChain) == X509ChainStatusFlags.PartialChain)
                {
                    return null;
                }

                return CertificateChainUtility.GetCertificateListFromChain(chain);
            }
        }

        private sealed class Errors
        {
            internal NuGetLogCode NoCertificate { get; }
            internal string NoCertificateString { get; }
            internal NuGetLogCode InvalidSignature { get; }
            internal string InvalidSignatureString { get; }
            internal NuGetLogCode ChainBuildingFailed { get; }

            internal Errors(
                NuGetLogCode noCertificate,
                string noCertificateString,
                NuGetLogCode invalidSignature,
                string invalidSignatureString,
                NuGetLogCode chainBuildingFailed)
            {
                NoCertificate = noCertificate;
                NoCertificateString = noCertificateString;
                InvalidSignature = invalidSignature;
                InvalidSignatureString = invalidSignatureString;
                ChainBuildingFailed = chainBuildingFailed;
            }
        }
#endif
    }
}