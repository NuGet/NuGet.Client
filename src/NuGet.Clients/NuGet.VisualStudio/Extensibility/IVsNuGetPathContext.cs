// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// NuGet path information specific to the current context (e.g. project context).
    /// </summary>
    [ComImport]
    [Guid("24A1A187-75EE-4296-A8B3-59F0E0707119")]
    public interface IVsNuGetPathContext
    {
        /// <summary>
        /// User package folder directory.
        /// </summary>
        string UserPackageFolder { get; }

        /// <summary>
        /// Fallback package folder locations.
        /// </summary>
        IReadOnlyList<string> FallbackPackageFolders { get; }
    }
}
