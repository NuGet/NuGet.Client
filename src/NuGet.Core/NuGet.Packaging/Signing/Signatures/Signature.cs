// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Package signature information.
    /// </summary>
    public class Signature
    {
        private readonly byte[] _data;

        private Signature(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; private set; }

        /// <summary>
        /// Additional counter signatures.
        /// </summary>
        public IReadOnlyList<Signature> AdditionalSignatures { get; private set; } = (new List<Signature>()).AsReadOnly();

        /// <summary>
        /// Signature manifest containing the hash of the content manifest.
        /// </summary>
        public SignatureManifest SignatureManifest { get; private set; }

        /// <summary>
        /// Save the signed cms signature to a stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream)
        {
            using (var ms = new MemoryStream(_data))
            {
                ms.CopyTo(stream);
            }
        }

        /// <summary>
        /// Retrieve the bytes of the signed cms signature.
        /// </summary>
        public byte[] GetBytes()
        {
            return _data;
        }

#if IS_DESKTOP
        public SignerInfoCollection SignerInfoCollection { get; private set; }

        public X509Certificate2Collection Certificates { get; private set; }

        public static Signature Load(SignedCms cms)
        {
            return new Signature(cms.Encode())
            {
                SignatureManifest = SignatureManifest.Load(cms.ContentInfo.Content),
                SignerInfoCollection = cms.SignerInfos,
                Certificates = cms.Certificates
            };
        }

        private static Signature Load(SignedCms cms, byte[] data)
        {
            if (data == null)
            {
                data = cms.Encode();
            }

            return new Signature(data)
            {
                SignatureManifest = SignatureManifest.Load(cms.ContentInfo.Content),
                SignerInfoCollection = cms.SignerInfos,
                Certificates = cms.Certificates
            };
        }

        public static Signature Load(Stream stream)
        {
            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return Load(ms.ToArray());
            }
        }

        public static Signature Load(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var cms = new SignedCms();
            cms.Decode(data);

            return Load(cms, data);
        }
#else
        private void VerifySignature(Signature signature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
