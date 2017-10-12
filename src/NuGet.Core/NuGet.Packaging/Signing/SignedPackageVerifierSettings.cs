// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Feed settings used to verify packages.
    /// </summary>
    public sealed class SignedPackageVerifierSettings
    {
        /// <summary>
        /// Allow packages that do not contain signatures.
        /// </summary>
        public bool AllowUnsigned { get; }

        /// <summary>
        /// Allow packages that are not trusted.
        /// </summary>
        public bool AllowUntrusted { get; }

        private SignedPackageVerifierSettings(bool allowUnsigned, bool allowUntrusted)
        {
            AllowUnsigned = allowUnsigned;
            AllowUntrusted = allowUntrusted;
        }

        /// <summary>
        /// Allow unsigned.
        /// </summary>
        public static SignedPackageVerifierSettings AllowAll { get; } = new SignedPackageVerifierSettings(allowUnsigned: true, allowUntrusted: true);

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings Default { get; } = AllowAll;

        /// <summary>
        /// Require all packages to be signed and valid.
        /// </summary>
        public static SignedPackageVerifierSettings RequireSigned { get; } = new SignedPackageVerifierSettings(allowUnsigned: false, allowUntrusted: false);

        /// <summary>
        /// Require all packages to be signed but allow untrusted packages that are valid.
        /// </summary>
        public static SignedPackageVerifierSettings RequireSignedAllowUntrusted { get; } = new SignedPackageVerifierSettings(allowUnsigned: false, allowUntrusted: true);
    }
}
