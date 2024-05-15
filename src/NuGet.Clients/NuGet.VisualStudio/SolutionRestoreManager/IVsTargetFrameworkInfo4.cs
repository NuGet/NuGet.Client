// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager
{
    public interface IVsTargetFrameworkInfo4
    {
        /// <summary>
        /// Target framework name in full format.
        /// </summary>
        string TargetFrameworkMoniker { get; }

        /// <summary>
        /// Collection of item types.
        /// e.g. PackageReference
        /// </summary>
        IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem2>> Items { get; }

        /// <summary>
        /// Collection of project level properties evaluated per each Target Framework,
        /// e.g. PackageTargetFallback.
        /// </summary>
        IReadOnlyDictionary<string, string> Properties { get; }
    }
}
