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
using NuGet.Packaging.Signing.DerEncoding;

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
            var valueOid = GetSignatureTypeOid(type);

            // DER encode the signature type Oid in a sequence.
            // CommitmentTypeQualifier ::= SEQUENCE {
            // commitmentTypeIdentifier CommitmentTypeIdentifier,
            // qualifier                  ANY DEFINED BY commitmentTypeIdentifier }
            var commitmentTypeData = DerEncoder.ConstructSequence(new List<byte[][]>() { DerEncoder.SegmentedEncodeOid(valueOid) });
            var data = new AsnEncodedData(Oids.CommitmentTypeIndication, commitmentTypeData);

            // Create an attribute
            return new CryptographicAttributeObject(
                oid: new Oid(Oids.CommitmentTypeIndication),
                values: new AsnEncodedDataCollection(data));
        }

        /// <summary>
        /// Oid -> SignatureType
        /// </summary>
        /// <param name="attribute">A commitment-type-indication attribute object.</param>
        /// <remarks>Unknown Oids are ignored. Throws for empty values and invalid combinations.</remarks>
        public static SignatureType GetCommitmentTypeIndication(CryptographicAttributeObject attribute)
        {
            if (!IsValidCommitmentTypeIndication(attribute))
            {
                throw new SignatureException(Strings.CommitmentTypeIndicationAttributeInvalid);
            }

            // Remove unknown values, these could be future values.
            // Invalid combinations and empty checks have already been done.
            var knownValues = GetCommitmentTypeIndicationRawValues(attribute)
                .Where(e => e != SignatureType.Unknown)
                .ToList();

            // Return the only recognized value.
            if (knownValues.Count == 1)
            {
                return knownValues[0];
            }

            // All values were unknown
            return SignatureType.Unknown;
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
        /// True if the commitment-type-indication value does not
        /// contain an invalid combination of values. Unknown
        /// values are ignored.
        /// </summary>
        public static bool IsValidCommitmentTypeIndication(CryptographicAttributeObject attribute)
        {
            var values = GetCommitmentTypeIndicationRawValues(attribute);

            // Zero values is invalid.
            if (values.Count < 1)
            {
                return false;
            }

            // Remove unknown values, these could be future values.
            var knownValues = values.Where(e => e != SignatureType.Unknown).ToList();

            // Currently the value must be a single value of author or repository. If multiple
            // known values exist then either there is a duplicate or both author and repository
            // was listed in the attribute.
            if (knownValues.Count > 1)
            {
                return false;
            }

            // A known or unknown value is present, and no invalid combinations exist.
            return true;
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
        /// CryptographicAttributeObject -> DerSequenceReader
        /// </summary>
        internal static DerSequenceReader ToDerSequenceReader(this CryptographicAttributeObject attribute)
        {
            var values = attribute.Values.ToList();

            if (values.Count != 1)
            {
                ThrowInvalidAttributeException(attribute);
            }

            return new DerSequenceReader(values[0].RawData);
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
            var values = new List<SignatureType>(1);
            var reader = attribute.ToDerSequenceReader();

            while (reader.HasData)
            {
                values.Add(GetSignatureType(reader.ReadOidAsString()));
            }

            return values;
        }
#endif
    }
}