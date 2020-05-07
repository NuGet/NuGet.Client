// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.Pkcs;

namespace NuGet.Packaging.Signing
{
    internal interface IRfc3161TimestampToken
    {
#if IS_SIGNING_SUPPORTED
        IRfc3161TimestampTokenInfo TokenInfo { get; }

        SignedCms AsSignedCms();
#endif
    }
}

