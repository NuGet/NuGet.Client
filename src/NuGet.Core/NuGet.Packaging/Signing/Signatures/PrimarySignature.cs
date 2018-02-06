// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public abstract class PrimarySignature : Signature
    {
#if IS_DESKTOP
        /// <summary>
        /// A SignedCms object holding the signature and SignerInfo.
        /// </summary>
        public SignedCms SignedCms { get; }

        /// <summary>
        /// Signature content.
        /// </summary>
        public SignatureContent SignatureContent { get; }

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
        public static PrimarySignature Load(SignedCms cms)
        {
            if (cms == null)
            {
                throw new ArgumentNullException(nameof(cms));
            }

            if (cms.SignerInfos.Count != 1)
            {
                throw new SignatureException(NuGetLogCode.NU3009, Strings.Error_NotOnePrimarySignature);
            }

            var signerInfo = cms.SignerInfos[0];
            var signatureType = AttributeUtility.GetSignatureType(signerInfo.SignedAttributes);

            VerifySigningCertificate(cms, signerInfo, SigningSpecifications.V1);

            return PrimarySignatureFactory.CreateSignature(cms, signatureType);
        }

        /// <summary>
        /// Create a signature based on a valid byte array to be decoded as a signed cms
        /// </summary>
        /// <param name="data">signature data</param>
        public static PrimarySignature Load(byte[] data)
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
        public static PrimarySignature Load(Stream stream)
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

        protected PrimarySignature(SignedCms signedCms, SignatureType signatureType)
            : base(GetSignerInfoIfSignedCmsNotNull(signedCms), signatureType)
        {
            SignedCms = signedCms;
            SignatureContent = SignatureContent.Load(SignedCms.ContentInfo.Content, SigningSpecifications.V1);
        }

        protected static void ThrowForInvalidAuthorSignature()
        {
            throw new SignatureException(NuGetLogCode.NU3011, Strings.InvalidPrimarySignature);
        }

        private static void VerifySigningCertificate(
            SignedCms signedCms,
            SignerInfo signerInfo,
            SigningSpecifications signingSpecifications)
        {
            var certificates = SignatureUtility.GetPrimarySignatureCertificates(
                signedCms,
                signerInfo,
                signingSpecifications);

            if (certificates == null || certificates.Count == 0)
            {
                ThrowForInvalidAuthorSignature();
            }
        }

        private static SignerInfo GetSignerInfoIfSignedCmsNotNull(SignedCms signedCms)
        {
            if (signedCms == null)
            {
                throw new ArgumentNullException(nameof(signedCms));
            }
            return signedCms.SignerInfos[0];
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
