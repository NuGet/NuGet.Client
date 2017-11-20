// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using System.Linq;

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string _signaturePath = ".signature.p7s";
        private const string _SHA256String = "SHA256";
        private const string _SHA384String = "SHA384";
        private const string _SHA512String = "SHA512";
        private const int _majorVersion = 0;
        private const int _minorVersion = 9;
        private const int _rsaPublicKeyMinLength = 2048;
        
        /// <summary>
        /// Allowed digest algorithms for signature and timestamp hashing.
        /// </summary>
        private static readonly string[] _allowedHashAlgorithms = new[]
        {
            _SHA256String,
            _SHA384String,
            _SHA512String
        };

        /// <summary>
        /// Allowed digest algorithm Oids for signature and timestamp hashing.
        /// </summary>
        private static readonly string[] _allowedHashAlgorithmOids = new[]
        {
            HashAlgorithmName.SHA256.ConvertToOidString(),
            HashAlgorithmName.SHA384.ConvertToOidString(),
            HashAlgorithmName.SHA512.ConvertToOidString()
        };

        private static readonly int[] _supportedMajorVersions = new[]
        {
            1
        };

        public override string SignaturePath => _signaturePath;

        public override string[] AllowedHashAlgorithms => _allowedHashAlgorithms;

        public override string[] AllowedHashAlgorithmOids => _allowedHashAlgorithmOids;

        public override int RSAPublicKeyMinLength => _rsaPublicKeyMinLength;

        public override int[] SupportedMajorVersions => _supportedMajorVersions;

        public SigningSpecificationsV1()
            : base()
        {
        }
    }
}
