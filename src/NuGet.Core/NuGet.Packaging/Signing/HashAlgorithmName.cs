// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// An enum of all accepted hash algorithm names.
    /// Any algorithm that cannot be mapped to this enum should be rejected in package signing.
    /// But this list should not be the only check. Each singature version may have different accepted formats.
    /// </summary>
    public enum HashAlgorithmName
    {
        SHA256,
        SHA384,
        SHA512
    }
}
