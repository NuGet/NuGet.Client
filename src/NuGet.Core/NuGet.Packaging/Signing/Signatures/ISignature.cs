// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public interface ISignature
    {
#if IS_DESKTOP
        SignatureType Type { get; }

        SignerInfo SignerInfo { get; }
#endif
    }
}
