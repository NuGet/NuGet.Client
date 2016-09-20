// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Contains project metadata needed for project restore operation
    /// </summary>
    [ComImport]
    [Guid("ab43992d-b977-436d-84c2-e76aeed20de2")]
    public interface IVsProjectRestoreInfo
    {
        /// <summary>
        /// Base intermediate path of the project
        /// </summary>
        string BaseIntermediatePath { get; }

        /// <summary>
        /// Target frameworks
        /// </summary>
        IEnumerable<IVsTargetFrameworkInfo> Frameworks { get; }
    }
}
