// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Package signature information.
    /// </summary>
    public sealed class Signature
    {
#if IS_DESKTOP
        private readonly Lazy<IReadOnlyList<Timestamp>> _timestamps;

        /// <summary>
        /// A SignedCms object holding the signature and SignerInfo.
        /// </summary>
        public SignedCms SignedCms { get; }

        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; }

        /// <summary>
        /// Signature content.
        /// </summary>
        public SignatureContent SignatureContent { get; }

        /// <summary>
        /// Signature timestamps.
        /// </summary>
        public IReadOnlyList<Timestamp> Timestamps => _timestamps.Value;

        /// <summary>
        /// SignerInfo for this signature.
        /// </summary>
        public SignerInfo SignerInfo => SignedCms.SignerInfos[0];

        private Signature(SignedCms signedCms, SignatureType signatureType)
        {
            SignedCms = signedCms ?? throw new ArgumentNullException(nameof(signedCms));
            SignatureContent = SignatureContent.Load(SignedCms.ContentInfo.Content, SigningSpecifications.V1);
            Type = signatureType;

            _timestamps = new Lazy<IReadOnlyList<Timestamp>>(() => GetTimestamps(SignerInfo));
        }

        /// <summary>
        /// Save the signed cms signature to a stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream)
        {
            using (var ms = new MemoryStream(SignedCms.Encode()))
            {
                ms.CopyTo(stream);
            }
        }

        /// <summary>
        /// Retrieve the bytes of the signed cms signature.
        /// </summary>
        public byte[] GetBytes()
        {
            return SignedCms.Encode();
        }

        /// <summary>
        /// Create a signature based on a valid signed cms
        /// </summary>
        /// <param name="cms">signature data</param>
        public static Signature Load(SignedCms cms)
        {
            if (cms == null)
            {
                throw new ArgumentNullException(nameof(cms));
            }

            if (cms.SignerInfos.Count != 1)
            {
                throw new SignatureException(NuGetLogCode.NU3009, Strings.Error_NotOnePrimarySignature);
            }

            var signingSpecifications = SigningSpecifications.V1;
            var signerInfo = cms.SignerInfos[0];
            var signatureType = AttributeUtility.GetSignatureType(signerInfo.SignedAttributes);

            VerifySigningCertificate(cms, signerInfo, signingSpecifications);

            if (signatureType == SignatureType.Author)
            {
                VerifySigningTimeAttribute(signerInfo);
            }

            return new Signature(cms, signatureType);
        }

        /// <summary>
        /// Create a signature based on a valid byte array to be decoded as a signed cms
        /// </summary>
        /// <param name="data">signature data</param>
        public static Signature Load(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var cms = new SignedCms();
            cms.Decode(data);

            return Load(cms);
        }

        /// <summary>
        /// Create a signature based on a valid byte stream to be decoded as a signed cms
        /// </summary>
        /// <param name="stream">signature data</param>
        public static Signature Load(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return Load(ms.ToArray());
            }
        }

        private static void VerifySigningTimeAttribute(SignerInfo signerInfo)
        {
            var attribute = signerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningTime);

            if (attribute == null)
            {
                ThrowForInvalidAuthorSignature();
            }
        }

        private static void VerifySigningCertificate(
            SignedCms signedCms,
            SignerInfo signerInfo,
            SigningSpecifications signingSpecifications)
        {
            var certificates = SignatureUtility.GetPrimarySignatureSigningCertificate(
                signedCms,
                signerInfo,
                signingSpecifications);

            if (certificates == null || certificates.Count == 0)
            {
                ThrowForInvalidAuthorSignature();
            }
        }

        private static void ThrowForInvalidAuthorSignature()
        {
            throw new SignatureException(NuGetLogCode.NU3011, Strings.InvalidPrimarySignature);
        }

        /// <summary>
        /// Get timestamps from the signer info
        /// </summary>
        /// <param name="signer"></param>
        /// <returns></returns>
        private static IReadOnlyList<Timestamp> GetTimestamps(SignerInfo signer)
        {
            var authorUnsignedAttributes = signer.UnsignedAttributes;

            var timestampList = new List<Timestamp>();

            foreach (var attribute in authorUnsignedAttributes)
            {
                if (string.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttribute, StringComparison.Ordinal))
                {
                    var timestampCms = new SignedCms();
                    timestampCms.Decode(attribute.Values[0].RawData);

                    var certificates = SignatureUtility.GetTimestampSignatureSigningCertificate(
                        timestampCms,
                        SigningSpecifications.V1);

                    if (certificates == null || certificates.Count == 0)
                    {
                        throw new SignatureException(NuGetLogCode.NU3029, Strings.InvalidTimestampSignature);
                    }

                    timestampList.Add(new Timestamp(timestampCms));
                }
            }

            return timestampList;
        }

#else
        /// <summary>
        /// Retrieve the bytes of the signed cms signature.
        /// </summary>
        public byte[] GetBytes()
        {
            throw new NotSupportedException();
        }
#endif
    }
}