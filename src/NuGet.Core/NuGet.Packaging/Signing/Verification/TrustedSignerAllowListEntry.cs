// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.Packaging.Signing
{
    public sealed class TrustedSignerAllowListEntry : CertificateHashAllowListEntry
    {
        /// <summary>
        /// List of allowed owners for a repository signature
        /// </summary>
        public IReadOnlyList<string> Owners { get; }

        public TrustedSignerAllowListEntry(
            VerificationTarget target,
            SignaturePlacement placement,
            string fingerprint,
            HashAlgorithmName algorithm,
            IReadOnlyList<string> owners = null)
            : base(target, placement, fingerprint, algorithm)
        {
            Owners = owners;
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) && obj is TrustedSignerAllowListEntry trustedSigner)
            {
                return Owners?.OrderBy(o => o).SequenceEqual(trustedSigner.Owners?.OrderBy(o => o)) ?? true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Placement);
            combiner.AddObject(Target);
            combiner.AddObject(Fingerprint);
            combiner.AddObject(FingerprintAlgorithm);

            if (Owners != null)
            {
                combiner.AddSequence(Owners);
            }

            return combiner.GetHashCode();
        }
    }
}
