// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// NuGet path information specific to the current context (e.g. project context).
    /// </summary>
    [ComImport]
    [Guid("24A1A187-75EE-4296-A8B3-59F0E0707119")]
    public interface IVsPathContext
    {
        /// <summary>
        /// User package folder directory. The path returned is an absolute path.
        /// </summary>
        string UserPackageFolder { get; }

        /// <summary>
        /// Fallback package folder locations. The paths (if any) in the returned list are absolute paths. If no
        /// fallback package folders are configured, an empty list is returned. The item type of this sequence is
        /// <see cref="string"/>.
        /// </summary>
        IEnumerable FallbackPackageFolders { get; }
    }
}
