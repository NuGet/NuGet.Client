// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string _folder = "testsigned";
        private const string _manifestPath = "testsigned/manifest.txt";
        private const string _signaturePath1 = "testsigned/a.sig";
        private const string _signaturePath2 = "testsigned/b.sig";

        /// <summary>
        /// These paths are allowed in the testsigned folder.
        /// </summary>
        private static readonly string[] _allowedPaths = new[]
        {
            _folder,
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
