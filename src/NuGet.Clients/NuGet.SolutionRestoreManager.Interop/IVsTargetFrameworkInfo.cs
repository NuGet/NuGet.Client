// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Immutable;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Contains target framework metadata needed for restore operation
    /// </summary>
    [ComImport]
    [Guid("9a1e969a-3e1e-4764-a48b-b823fe716fab")]
    public interface IVsTargetFrameworkInfo
    {
        /// <summary>
        /// Friendly project name.
        /// </summary>
        string TargetFrameworkMoniker { get; }

        /// <summary>
        /// Project references metadata
        /// </summary>
        IImmutableDictionary<string, IImmutableDictionary<string, string>> ProjectReferences { get; }

        /// <summary>
        /// Package references metadata
        /// </summary>
        IImmutableDictionary<string, IImmutableDictionary<string, string>> PackageReferences { get; }
    }
}
