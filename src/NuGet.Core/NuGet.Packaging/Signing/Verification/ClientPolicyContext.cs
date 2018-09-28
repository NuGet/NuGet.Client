// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    public class ClientPolicyContext
    {
        /// <summary>
        /// Current policy the client is on.
        /// </summary>
        //public SignatureRevocationMode Policy { get; }

        /// <summary>
        /// Verification settings corresponding the current client policy.
        /// </summary>
        public SignedPackageVerifierSettings VerifierSettings { get; }

        /// <summary>
        /// List of signatures allowed in verification.
        /// </summary>
        public IReadOnlyCollection<VerificationAllowListEntry> AllowList { get; }

        /// <summary>
        /// Require AllowList to not be null or empty
        /// </summary>
        public bool RequireNonEmptyAllowList { get; }

        public ClientPolicyContext(SignedPackageVerifierSettings verifierSettings, IReadOnlyCollection<VerificationAllowListEntry> allowList = null, bool requireNonEmptyAllowList = false)
        {
            VerifierSettings = verifierSettings ?? throw new ArgumentNullException(nameof(verifierSettings));
            AllowList = allowList;
            RequireNonEmptyAllowList = requireNonEmptyAllowList;
        }
    }
}
