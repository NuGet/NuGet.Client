// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.VisualStudio
{
    internal class PackageReferenceMutex
    {
        internal static readonly object CounterfactualLock = new();
        internal static bool IsCounterfactualEmitted = false;
    }
}
