// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string _folder = "testsigned";
        private const string _manifestPath = "testsigned/manifest.txt";
        private const string _signaturePath1 = "testsigned/a.sig";
        private const string _signaturePath2 = "testsigned/b.sig";
        private const string _SHA256String = "SHA256";
        private const string _SHA384String = "SHA384";
        private const string _SHA512String = "SHA512";

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
            HashAlgorithmName.SHA256.ConvertOidString(),
            HashAlgorithmName.SHA256.ConvertOidString(),
            HashAlgorithmName.SHA256.ConvertOidString()
        };

        /// <summary>
        /// These paths are allowed in the testsigned folder.
        /// </summary>
        private static readonly string[] _allowedPaths = new[]
        {
            _manifestPath,
            _signaturePath1,
            _signaturePath2
        };

        /// <summary>
        /// These paths MUST exist for the package to be
        /// considered signed.
        /// </summary>
        private static readonly string[] _requiredPaths = new[]
        {
            _signaturePath1
        };

        public override string[] AllowedPaths => _allowedPaths;

        public override string[] AllowedHashAlgorithms => _allowedHashAlgorithms;

        public override string[] AllowedHashAlgorithmOids => _allowedHashAlgorithmOids;

        public override string[] RequiredPaths => _requiredPaths;

        public string ManifestPath => _manifestPath;

        public string SignaturePath1 => _signaturePath1;

        public string SignaturePath2 => _signaturePath2;

        public SigningSpecificationsV1()
            : base(_folder)
        {
        }
    }
}
