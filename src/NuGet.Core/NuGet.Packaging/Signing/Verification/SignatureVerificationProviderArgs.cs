// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureVerificationProviderArgs
    {
        public IReadOnlyList<VerificationAllowListEntry> AllowList { get; }

        public SignatureVerificationProviderArgs()
        {
            AllowList = new List<VerificationAllowListEntry>();
        }

        public SignatureVerificationProviderArgs(IReadOnlyList<VerificationAllowListEntry> allowList)
        {
            AllowList = allowList ?? throw new ArgumentNullException(nameof(allowList));
        }
    }
}
