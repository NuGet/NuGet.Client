// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;

namespace NuGet.Packaging.Signing
{
    public class TrustedSourceAllowListEntry : VerificationAllowListEntry
    {
        public TrustedSource Source { get; }

        public TrustedSourceAllowListEntry(TrustedSource source)
            : base(VerificationTarget.Repository, SignaturePlacement.PrimarySignature|SignaturePlacement.Countersignature)
        {
            Source = source;
        }
    }
}