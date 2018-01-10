// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    public static class AttributeUtility
    {
#if IS_DESKTOP
        /// <summary>
        /// Create a CommitmentTypeIndication attribute.
        /// https://tools.ietf.org/html/rfc5126.html#section-5.11.1
        /// </summary>
        /// <param name="type">The signature type.</param>
        public static CryptographicAttributeObject CreateCommitmentTypeIndication(SignatureType type)
        {
            // SignatureType -> Oid
            var oid = GetSignatureTypeOid(type);

            var commitmentTypeQualifier = CommitmentTypeQualifier.Create(new Oid(oid));
            var value = new AsnEncodedData(Oids.CommitmentTypeIndication, commitmentTypeQualifier.Encode());

            return new CryptographicAttributeObject(
                new Oid(Oids.CommitmentTypeIndication),
                new AsnEncodedDataCollection(value));
        }

        /// <summary>
        /// Gets the signature type from a commitment-type-indication attribute object.
        /// </summary>
        /// <param name="attribute">A commitment-type-indication attribute object.</param>
        /// <remarks>Unknown OIDs are ignored.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="attribute" /> is <c>null</c>.</exception>
        /// <exception cref="SignatureException">Thrown if <paramref name="attribute" /> is invalid.</exception>
        public static SignatureType GetCommitmentTypeIndication(CryptographicAttributeObject attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (attribute.Oid.Value != Oids.CommitmentTypeIndication)
            {
                throw new SignatureException(Strings.CommitmentTypeIndicationAttributeInvalid);
            }

            var values = GetCommitmentTypeIndicationRawValues(attribute);

            // Remove unknown values, these could be future values.
            var knownValues = values.Where(e => e != SignatureType.Unknown).Distinct().ToList();

            if (knownValues.Count == 0)
            {
                return SignatureType.Unknown;
            }

            // Author and repository values are mutually exclusive in the same signature.
            // If multiple distinct known values exist then the attribute is invalid.
            if (knownValues.Count > 1)
            {
                throw new SignatureException(Strings.CommitmentTypeIndicationAttributeInvalidCombination);
            }

            return knownValues[0];
        }

        internal static SignatureType GetCommitmentTypeIndication(SignerInfo signer)
        {
            var commitmentTypeIndication = signer.SignedAttributes.GetAttributeOrDefault(Oids.CommitmentTypeIndication);
            if (commitmentTypeIndication != null)
            {
                return GetCommitmentTypeIndication(commitmentTypeIndication);
            }

            return SignatureType.Unknown;
        }

        /// <summary>
        /// Oid -> SignatureType
        /// </summary>
        /// <param name="oid">The commitment-type-indication value.</param>
        public static SignatureType GetSignatureType(string oid)
        {
            switch (oid)
            {
                case Oids.CommitmentTypeIdentifierProofOfOrigin:
                    return SignatureType.Author;
                case Oids.CommitmentTypeIdentifierProofOfReceipt:
                    return SignatureType.Repository;
                default:
                    return SignatureType.Unknown;
            }
        }

        /// <summary>
        /// SignatureType -> Oid
        /// </summary>
        /// <param name="signatureType">The signature type.</param>
        public static string GetSignatureTypeOid(SignatureType signatureType)
        {
            switch (signatureType)
            {
                case SignatureType.Author:
                    return Oids.CommitmentTypeIdentifierProofOfOrigin;
                case SignatureType.Repository:
                    return Oids.CommitmentTypeIdentifierProofOfReceipt;
                default:
                    throw new ArgumentException(nameof(signatureType));
            }
        }

        /// <summary>
        /// Create a signing-certificate-v2 from a certificate.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="hashAlgorithm">The hash algorithm for the signing-certificate-v2 attribute.</param>
        public static CryptographicAttributeObject CreateSigningCertificateV2(
            X509Certificate2 certificate,
            Common.HashAlgorithmName hashAlgorithm)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var signingCertificateV2 = SigningCertificateV2.Create(certificate, hashAlgorithm);
            var bytes = signingCertificateV2.Encode();

            var data = new AsnEncodedData(Oids.SigningCertificateV2, bytes);

            return new CryptographicAttributeObject(
                new Oid(Oids.SigningCertificateV2),
                new AsnEncodedDataCollection(data));
        }

        /// <summary>
        /// Returns the first attribute if the Oid is found.
        /// Returns null if the attribute is not found.
        /// </summary>
        internal static CryptographicAttributeObject GetAttributeOrDefault(this CryptographicAttributeObjectCollection attributes, string oid)
        {
            if (oid == null)
            {
                throw new ArgumentNullException(nameof(oid));
            }

            foreach (var attribute in attributes)
            {
                if (StringComparer.Ordinal.Equals(oid, attribute.Oid.Value))
                {
                    return attribute;
                }
            }

            return null;
        }

        /// <summary>
        /// Throw a signature exception due to an invalid attribute. This is used for unusual situations
        /// where the format is corrupt.
        /// </summary>
        private static void ThrowInvalidAttributeException(CryptographicAttributeObject attribute)
        {
            throw new SignatureException(string.Format(CultureInfo.CurrentCulture, Strings.SignatureContainsInvalidAttribute, attribute.Oid.Value));
        }

        /// <summary>
        /// Enumerate AsnEncodedDataCollection
        /// </summary>
        private static List<AsnEncodedData> ToList(this AsnEncodedDataCollection collection)
        {
            var values = new List<AsnEncodedData>();

            foreach (var value in collection)
            {
                values.Add(value);
            }

            return values;
        }

        /// <summary>
        /// Attribute -> SignatureType values with no validation.
        /// </summary>
        private static List<SignatureType> GetCommitmentTypeIndicationRawValues(CryptographicAttributeObject attribute)
        {
            // Most packages should have either 0 or 1 signature types.
            var values = new List<SignatureType>(capacity: 1);

            /*
                From RFC 5126 (https://tools.ietf.org/html/rfc5126.html#section-5.11.1):

                    CommitmentTypeIndication ::= SEQUENCE {
                      commitmentTypeId CommitmentTypeIdentifier,
                      commitmentTypeQualifier SEQUENCE SIZE (1..MAX) OF
                                     CommitmentTypeQualifier OPTIONAL}

                    CommitmentTypeIdentifier ::= OBJECT IDENTIFIER
            */

            // CryptographicAttributeObject.Values represent the values in the commitmentTypeQualifier sequence above.
            // CryptographicAttributeObject forces its Values property to be an empty collection, so it is impossible
            // from CryptographicAttributeObject to distinguish between the sequence being absent, which is permitted
            // here, and the sequence being empty, which is invalid here.
            // We'll err on the side of leniency and treat an empty sequence like an absent sequence.

            foreach (var value in attribute.Values)
            {
                var qualifier = CommitmentTypeQualifier.Read(value.RawData);
                var signatureType = GetSignatureType(qualifier.CommitmentTypeIdentifier.Value);

                values.Add(signatureType);
            }

            return values;
        }
#endif
    }
}