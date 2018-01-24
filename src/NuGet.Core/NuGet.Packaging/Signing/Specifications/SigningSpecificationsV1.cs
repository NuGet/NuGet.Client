// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string _signaturePath = ".signature.p7s";
        private const int _rsaPublicKeyMinLength = 2048;
        private static readonly Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Allowed digest algorithms for signature and timestamp hashing.
        /// </summary>
        private static readonly HashAlgorithmName[] _allowedHashAlgorithms = new[]
        {
            HashAlgorithmName.SHA256,
            HashAlgorithmName.SHA384,
            HashAlgorithmName.SHA512
        };

        /// <summary>
        /// Allowed digest algorithm Oids for signature and timestamp hashing.
        /// </summary>
        private static readonly string[] _allowedHashAlgorithmOids = _allowedHashAlgorithms.Select(hash => hash.ConvertToOidString()).ToArray();

        /// <summary>
        /// Gets the signature format version.
        /// </summary>
        public override string Version => "1";

        public override string SignaturePath => _signaturePath;

        public override HashAlgorithmName[] AllowedHashAlgorithms => _allowedHashAlgorithms;

        public override string[] AllowedHashAlgorithmOids => _allowedHashAlgorithmOids;

        public override int RSAPublicKeyMinLength => _rsaPublicKeyMinLength;

        public override Encoding Encoding => _encoding;
        
        public SigningSpecificationsV1()
            : base()
        {
        }
    }
}