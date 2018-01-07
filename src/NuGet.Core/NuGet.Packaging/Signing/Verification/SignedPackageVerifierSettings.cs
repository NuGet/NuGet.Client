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

        public bool AllowIgnoreTimestamp { get; }

        public bool FailWithMultipleTimestamps { get; }

        public bool AllowNoTimestamp { get; }

        public SignedPackageVerifierSettings(bool allowUnsigned, bool allowUntrusted, bool allowIgnoreTimestamp, bool failWithMultupleTimestamps, bool allowNoTimestamp)
        {
            AllowUnsigned = allowUnsigned;
            AllowUntrusted = allowUntrusted;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            FailWithMultipleTimestamps = failWithMultupleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
        }

        /// <summary>
        /// Allow unsigned.
        /// </summary>
        public static SignedPackageVerifierSettings AllowAll { get; } = new SignedPackageVerifierSettings(allowUnsigned: true, allowUntrusted: true, allowIgnoreTimestamp: true, failWithMultupleTimestamps: false, allowNoTimestamp: true);

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings Default { get; } = AllowAll;

        /// <summary>
        /// Default policy for scenarios in VS
        /// </summary>
        public static SignedPackageVerifierSettings VSClientDefaultPolicy { get; } = new SignedPackageVerifierSettings(allowUnsigned: true, allowUntrusted: true, allowIgnoreTimestamp: true, failWithMultupleTimestamps: false, allowNoTimestamp: true);

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command
        /// </summary>
        public static SignedPackageVerifierSettings VerifyCommandDefaultPolicy { get; } = new SignedPackageVerifierSettings(allowUnsigned: false, allowUntrusted: false, allowIgnoreTimestamp: false, failWithMultupleTimestamps: false, allowNoTimestamp: true);
    }
}
