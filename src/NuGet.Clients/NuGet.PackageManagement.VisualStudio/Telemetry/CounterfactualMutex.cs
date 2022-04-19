// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This class is needed to avoid generic type issues and static variables in <see cref="PackageReferenceProject{T, U}" />
    /// and to keep all counterfactual flags in one place
    /// </summary>
    internal class CounterfactualMutex
    {
        internal static int CounterfactualEmittedFlag = 0;
        internal static int PMUICounterfactualEmittedFlag = 0;
    }
}
