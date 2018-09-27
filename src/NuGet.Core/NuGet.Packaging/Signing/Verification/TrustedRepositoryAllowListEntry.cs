// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class TrustedRepositoryAllowListEntry : CertificateHashAllowListEntry
    {
        /// <summary>
        /// List of allowed owners for a repository signature
        /// </summary>
        public IReadOnlyList<string> Owners { get; }

        public TrustedRepositoryAllowListEntry(string fingerprint, HashAlgorithmName algorithm, IList<string> owners)
            : base(VerificationTarget.Repository, SignaturePlacement.Any, fingerprint, algorithm)
        {
            Owners = owners?.ToList();
        }
    }
}
