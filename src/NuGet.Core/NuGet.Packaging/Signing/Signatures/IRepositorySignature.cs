// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    public interface IRepositorySignature
    {
#if IS_DESKTOP
        Uri V3ServiceIndexUrl { get; }

        IReadOnlyList<string> PackageOwners { get; }
#endif
    }
}
