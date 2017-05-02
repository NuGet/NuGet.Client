// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary>
    /// This enum is used to quantify NuGet error and wanring codes. 
    /// Format - NUxyzw where NU is the profix indicating NuGet and xyzw is a 4 digit code
    /// 
    /// Numbers - xyzw
    ///     x - 'x' is the largest digit and should be used to quantify a set of errors.
    ///         For example 1yzw are set of restore related errors and no other code path should use the range 1000 to 1999 for errors or warnings.
    ///         
    ///     y - 'y' is the second largest digit and should be used for sub sections withing a broad category.
    ///     
    ///         For example 12zw cvould be http related errors.
    ///         Further 'y' = 0-4 shoudl be used for errors and 'y' = 5-9 should be warnings.
    ///         
    ///     zw - 'zw' are the least two digit.
    ///         These could be used for different errors or warnings within the broad categories set by digits 'xy'.
    /// </summary>
    /// 
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
        NU2504
    }
}