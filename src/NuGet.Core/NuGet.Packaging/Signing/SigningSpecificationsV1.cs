// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public sealed class SigningSpecificationsV1 : SigningSpecifications
    {
        private const string Folder = "testsigned";

        /// <summary>
        /// These paths are allowed in the testsigned folder.
        /// </summary>
        private static readonly string[] _allowedPaths = new[]
        {
            "testsigned/manifest.txt",
            "testsigned/a.sig",
            "testsigned/b.sig"
        };

        /// <summary>
        /// These paths MUST exist for the package to be
        /// considered signed.
        /// </summary>
        private static readonly string[] _requiredPaths = new[]
        {
            "testsigned/a.sig"
        };

        public override string[] AllowedPaths => _allowedPaths;

        public override string[] RequiredPaths => _requiredPaths;

        public SigningSpecificationsV1()
            : base(Folder)
        {
        }
    }
}
