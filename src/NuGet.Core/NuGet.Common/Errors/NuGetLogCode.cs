// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    public enum NuGetLogCode
    {
        NU1000 = 1000, // For cases do not fit into the cases below.
        NU1001, // Actual errors start here
        NU1002,

        /// <summary>
        /// Dependency bumped up
        /// </summary>
        NU2501 = 2501,

        /// <summary>
        /// Non-exact match on dependency range due to non inclusive minimum bound.
        /// </summary>
        NU2502,

        /// <summary>
        /// Non-exact match on dependency range due to missing package version.
        /// </summary>
        NU2503,

        /// <summary>
        /// Project dependency does not include a lower bound.
        /// </summary>
        NU2504,
    }
}