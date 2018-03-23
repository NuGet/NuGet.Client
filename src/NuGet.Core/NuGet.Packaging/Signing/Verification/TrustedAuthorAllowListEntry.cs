// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class TrustedAuthorAllowListEntry : VerificationAllowListEntry
    {
        // TODO: Add trusted author info

        public TrustedAuthorAllowListEntry()
            : base(VerificationTarget.Author, SignaturePlacement.PrimarySignature)
        {
        }
    }
}