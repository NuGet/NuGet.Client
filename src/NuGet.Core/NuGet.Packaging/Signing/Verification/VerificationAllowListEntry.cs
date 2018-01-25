// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public abstract class VerificationAllowListEntry
    {
        public VerificationTarget VerificationTarget { get; }

        public VerificationAllowListEntry(VerificationTarget target)
        {
            VerificationTarget = target;
        }
    }
}
