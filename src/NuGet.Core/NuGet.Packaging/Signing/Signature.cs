// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

#if NET46
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
#endif

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Package signature information.
    /// </summary>
    public class Signature
    {
        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; set; }

        /// <summary>
        /// Signature friendly name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Manifest hash.
        /// </summary>
        public string ManifestHash { get; set; }

        /// <summary>
        /// Actual signature bytes.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Additional counter signatures.
        /// </summary>
        public IReadOnlyList<Signature> AdditionalSignatures { get; set; } = new List<Signature>();

        /// <summary>
        /// TEMPORARY - trust result to return.
        /// </summary>
        public SignatureVerificationStatus TestTrust { get; set; }

#if NET46
        public SignerInfoCollection SignerInfoCollection { get; set; }

        public X509Certificate2Collection Certificates { get; set; }

        public static Signature FromStream(Stream stream)
        {
            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return FromBytes(ms.ToArray());
            }
        }

        public static Signature FromBytes(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var signature = new Signature();

            var cms = new SignedCms();
            cms.Decode(data);

            var content = cms.ContentInfo.Content;

            signature.ManifestHash = ASCIIEncoding.ASCII.GetString(content);

            signature.SignerInfoCollection = cms.SignerInfos;
            signature.Certificates = cms.Certificates;

            return signature;
        }
#else
        private void VerifySignature(Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
