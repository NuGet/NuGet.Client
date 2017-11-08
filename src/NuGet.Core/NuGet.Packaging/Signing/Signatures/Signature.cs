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

#if IS_DESKTOP

        /// <summary>
        /// A SignedCms object holding the signature and SignerInfo.
        /// </summary>
        public SignedCms SignedCms { get; }

        /// <summary>
        /// Index to the SignerInfo object in the SignedCms.SignInfos.
        /// The index is used to indicate the SignerInfo for this Signature.
        /// </summary>
        public int SignerInfoIndex { get; }

        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; }

        /// <summary>
        /// Signature manifest containing the hash of the content manifest.
        /// </summary>
        public SignatureManifest SignatureManifest { get; }

        /// <summary>
        /// SignerInfo for this signature.
        /// </summary>
        public SignerInfo SignerInfo => SignedCms.SignerInfos[SignerInfoIndex];

        private Signature(SignedCms signedCms, int signerInfoIndex)
        {
            SignedCms = signedCms ?? throw new ArgumentNullException(nameof(signedCms));
            SignerInfoIndex = signerInfoIndex;
            SignatureManifest = SignatureManifest.Load(SignedCms.ContentInfo.Content);
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

        public static Signature Load(SignedCms cms, int signerInfoIndex)
        {
            return new Signature(cms, signerInfoIndex);
        }

        private static Signature Load(SignedCms cms)
        {
            return new Signature(cms, signerInfoIndex: 0);
        }

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

        public static Signature Load(Stream stream)
        {
            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return Load(ms.ToArray());
            }
        }
#else
        private void VerifySignature(Signature signature)
        {
            throw new NotSupportedException();
        }

        public byte[] GetBytes()
        {
            throw new NotSupportedException();
        }
#endif
    }
}
