// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represent data from an assets file (project.assets.json).
    /// </summary>
    internal class RestoreGraphRead
    {
        /// <summary>
        /// Package spec associated with the reading
        /// </summary>
        public PackageSpec PackageSpec { get; }

        /// <summary>
        /// <c>targets</c> section from assets file. One entry for each target framework in project.
        /// Can be <c>null</c> if assets file is not found
        /// </summary>
        public IReadOnlyList<LockFileTarget> TargetsList { get; }

        /// <summary>
        /// Indicates if the reading was retrieved from internal cache, otherwise, 
        /// </summary>
        public bool IsCacheHit { get; }

        public RestoreGraphRead(PackageSpec packageSpec, IReadOnlyList<LockFileTarget> targetsList, bool isCacheHit)
        {
            TargetsList = targetsList;
            IsCacheHit = isCacheHit;
            PackageSpec = packageSpec;
        }
    }
}
