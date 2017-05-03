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
    ///         
    /// Groups:
    /// 1000 - Restore
    /// 
    /// Sub groups:
    /// 1000/1500 Input
    /// 1100/1600 Resolver
    /// 1200/1700 Compat
    /// 1300/1800 Feed
    /// 1400/1900 Package
    /// </summary>
    public enum NuGetLogCode
    {
        /// <summary>
        /// Undefined error
        /// </summary>
        NU1000 = 1000,

        /// <summary>
        /// Project has zero target frameworks.
        /// </summary>
        NU1001 = 1001,

        /// <summary>
        /// 
        /// </summary>
        NU1002 = 1002,

        /// <summary>
        /// Unable to resolve package
        /// TODO split this up into the following errors.
        /// </summary>
        NU1101 = 1101,
        NU1102 = 1102,
        NU1103 = 1103,
        NU1104 = 1104,
        NU1105 = 1105,

        /// <summary>
        /// Resolver conflict
        /// </summary>
        NU1106 = 1106,

        /// <summary>
        /// Dependency project has an incompatible framework.
        /// </summary>
        NU1201 = 1201,

        /// <summary>
        /// Dependency package does not contain assets for the current framework.
        /// </summary>
        NU1202 = 1202,

        /// <summary>
        /// un-matched reference assemblies
        /// </summary>
        NU1203 = 1203,

        /// <summary>
        /// Undefined warning
        /// </summary>
        NU1500 = 1500,

        /// <summary>
        /// Missing restore target.
        /// </summary>
        NU1501 = 1501,

        /// <summary>
        /// Unknown compatibility profile
        /// </summary>
        NU1502 = 1502,

        /// <summary>
        /// Dependency bumped up
        /// </summary>
        NU1601 = 2501,

        /// <summary>
        /// Non-exact match on dependency range due to non inclusive minimum bound.
        /// </summary>
        NU1602,

        /// <summary>
        /// Non-exact match on dependency range due to missing package version.
        /// </summary>
        NU1603,

        /// <summary>
        /// Project dependency does not include a lower bound.
        /// </summary>
        NU1604,

        /// <summary>
        /// Package dependency downgraded.
        /// </summary>
        NU1605 = 1605,

        /// <summary>
        /// Circular dependency.
        /// </summary>
        NU1606 = 1606,

        /// <summary>
        /// Version conflict.
        /// </summary>
        NU1607 = 1607,

        /// <summary>
        /// Fallback framework used.
        /// </summary>
        NU1701,

        /// <summary>
        /// Feed error converted to a warning when ignoreFailedSources is true.
        /// </summary>
        NU1801,
    }
}