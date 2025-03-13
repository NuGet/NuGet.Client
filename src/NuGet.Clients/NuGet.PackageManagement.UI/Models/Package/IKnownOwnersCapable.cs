// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

# nullable enable

using System.Collections.Generic;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public interface IKnownOwnersCapable
    {
        IReadOnlyList<KnownOwner>? KnownOwners { get; }
    }
}
