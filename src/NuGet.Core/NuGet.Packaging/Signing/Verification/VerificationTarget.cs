// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    [Flags]
    public enum VerificationTarget
    {
        Primary     = 0x000001,
        Repository  = 0x000010
    }
}
