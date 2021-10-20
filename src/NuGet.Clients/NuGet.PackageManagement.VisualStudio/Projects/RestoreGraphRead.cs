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
        /// Indicates if the reading was retrieved from internal cache, otherwise, <c>false</c>
        /// </summary>
        public bool IsCacheHit { get; }

        public RestoreGraphRead(PackageSpec packageSpec, bool isCacheHit)
        {
            IsCacheHit = isCacheHit;
            PackageSpec = packageSpec;
        }
    }
}
