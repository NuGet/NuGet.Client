// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Feed settings used to verify packages.
    /// </summary>
    public sealed class SignVerifierSettings
    {
        public bool AllowUnsigned { get; }

        private SignVerifierSettings(bool allowUnsigned)
        {
            AllowUnsigned = allowUnsigned;
        }

        /// <summary>
        /// Allow unsigned.
        /// </summary>
        public static SignVerifierSettings AllowAll { get; } = new SignVerifierSettings(allowUnsigned: true);

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignVerifierSettings Default { get; } = AllowAll;

        /// <summary>
        /// Require all packages to be signed and valid.
        /// </summary>
        public static SignVerifierSettings RequireSigned { get; } = new SignVerifierSettings(allowUnsigned: false);
    }
}
