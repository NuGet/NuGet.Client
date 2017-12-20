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
    ///         Further 'y' = 0-4 should be used for errors and 'y' = 5-9 should be warnings.
    ///         
    ///     zw - 'zw' are the least two digit.
    ///         These could be used for different errors or warnings within the broad categories set by digits 'xy'.
    ///         
    /// Groups:
    /// 1000      - Restore
    /// 3000/3500 - Signing
    /// 
    /// Sub groups for Restore:
    /// error/warning - Reason
    /// 1000/1500     - Input
    /// 1100/1600     - Resolver
    /// 1200/1700     - Compat
    /// 1300/1800     - Feed
    /// 1400/1900     - Package
    ///
    /// Sub groups for Signing:
    /// error/warning - Reason
    /// 3000/3500     - Input
    /// 3010/3510     - Package and package signature file
    /// 3020/3520     - Primary certificate
    /// 3030/3530     - Primary signature
    /// 3040/3540     - Timestamp certificate
    /// 3050/3550     - Timestamp signature
    /// </summary>
    public enum NuGetLogCode
    {
        /// <summary>
        /// Do not display the code.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Undefined error
        /// </summary>
        NU1000 = 1000,

        /// <summary>
        /// Project has zero target frameworks.
        /// </summary>
        NU1001 = 1001,

        /// <summary>
        /// Invalid combination with CLEAR
        /// </summary>
        NU1002 = 1002,

        /// <summary>
        /// Invalid combination of PTF and ATF
        /// </summary>
        NU1003 = 1003,

        /// <summary>
        /// Unable to resolve package, generic message for unknown type constraints.
        /// </summary>
        NU1100 = 1100,

        /// <summary>
        /// No versions of the package exist on any of the sources.
        /// </summary>
        NU1101 = 1101,

        /// <summary>
        /// Versions of the package exist, but none are in the range.
        /// </summary>
        NU1102 = 1102,

        /// <summary>
        /// Range does not allow prerelease packages and only prerelease versions were found
        /// within the range.
        /// </summary>
        NU1103 = 1103,

        /// <summary>
        /// Project path does not exist on disk.
        /// </summary>
        NU1104 = 1104,

        /// <summary>
        /// Project reference was not in the dg spec.
        /// </summary>
        NU1105 = 1105,

        /// <summary>
        /// Resolver conflict
        /// </summary>
        NU1106 = 1106,

        /// <summary>
        /// Version conflict.
        /// </summary>
        NU1107 = 1107,

        /// <summary>
        /// Circular dependency.
        /// </summary>
        NU1108 = 1108,

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
        /// Package MinClientVersion did not match.
        /// </summary>
        NU1401 = 1401,

        /// <summary>
        /// Package Signature is invalid
        /// </summary>
        NU1410 = 1410,

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
        /// Skipping project that does not support restore.
        /// </summary>
        NU1503 = 1503,

        /// <summary>
        /// Dependency bumped up
        /// </summary>
        NU1601 = 1601,

        /// <summary>
        /// Non-exact match on dependency range due to non inclusive minimum bound.
        /// </summary>
        NU1602 = 1602,

        /// <summary>
        /// Non-exact match on dependency range due to missing package version.
        /// </summary>
        NU1603 = 1603,

        /// <summary>
        /// Project dependency does not include a lower bound.
        /// </summary>
        NU1604 = 1604,

        /// <summary>
        /// Package dependency downgraded.
        /// </summary>
        NU1605 = 1605,

        // These codes have been moved and should not be reused.
        // NU1606 -> NU1108
        // NU1607 -> NU1107

        /// <summary>
        /// Version is higher than upper bound.
        /// </summary>
        NU1608 = 1608,

        /// <summary>
        /// Fallback framework used.
        /// </summary>
        NU1701 = 1701,

        /// <summary>
        /// Feed error converted to a warning when ignoreFailedSources is true.
        /// </summary>
        NU1801 = 1801,

        /// <summary>
        /// Undefined signature error
        /// </summary>
        NU3000 = 3000,

        /// <summary>
        /// Invalid Input error
        /// </summary>
        NU3001 = 3001,

        /// <summary>
        /// Invalid package error
        /// </summary>
        NU3002 = 3002,

        /// <summary>
        /// Invalid number of certificates were found while signing package
        /// </summary>
        NU3003 = 3003,

        /// <summary>
        /// Package signature is invalid
        /// </summary>
        NU3005 = 3005,

        /// <summary>
        /// Package is not signed
        /// </summary>
        NU3006 = 3006,

        /// <summary>
        /// ZIP entry is either not a regular file or is not stored (no compression)
        /// </summary>
        NU3011 = 3011,

        /// <summary>
        /// Unsupported signature format version
        /// </summary>
        NU3012 = 3012,

        /// <summary>
        /// Primary signature has an unsuported hash algorithm
        /// </summary>
        NU3013 = 3013,

        /// <summary>
        /// Primary signature count not 1
        /// </summary>
        NU3014 = 3014,

        /// <summary>
        /// Package integrity check failed
        /// </summary>
        NU3015 = 3015,

        /// <summary>
        /// Primary signature does not have certificate
        /// </summary>
        NU3020 = 3020,

        /// <summary>
        /// Chain building failed for primary signature
        /// </summary>
        NU3021 = 3021,

        /// <summary>
        /// Unsupported signature algorithm
        /// </summary>
        NU3022 = 3022,

        /// <summary>
        /// Unsupported public key length
        /// </summary>
        NU3023 = 3023,
        
        /// <summary>
        /// Certificate not yet valid
        /// </summary>
        NU3024 = 3024,

        /// <summary>
        /// Primary signature verification failed
        /// </summary>
        NU3030 = 3030,

        /// <summary>
        /// Timestamp with no certificate
        /// </summary>
        NU3040 = 3040,

        /// <summary>
        /// Chain building failed for timestamp
        /// </summary>
        NU3041 = 3041,

        /// <summary>
        /// Timestamp certificate not yet effective
        /// </summary>
        NU3044 = 3044,

        /// <summary>
        /// Invalid timestamp in signature
        /// </summary>
        NU3050 = 3050,

        /// <summary>
        /// Timestamp nonce mismatch
        /// </summary>
        NU3051 = 3051,

        /// <summary>
        /// Timestamp unsupported algorithm
        /// </summary>
        NU3052 = 3052,

        /// <summary>
        /// Timestamp integrity check
        /// </summary>
        NU3053 = 3053,

        /// <summary>
        /// Undefined signature warning
        /// </summary>
        NU3500 = 3500,

        /// <summary>
        /// Untrusted root warning
        /// </summary>
        NU3501 = 3501,

        /// <summary>
        /// Signature information unavailable warning
        /// </summary>
        NU3502 = 3502,

        /// <summary>
        /// Primary signature has an unsuported hash algorithm
        /// </summary>
        NU3513 = 3513,

        /// <summary>
        /// Timestamp url not passed to sign command
        /// </summary>
        NU3521 = 3521,

        /// <summary>
        /// Certificate not yet valid
        /// </summary>
        NU3524 = 3524,

        /// <summary>
        /// Primary signature verification failed
        /// </summary>
        NU3530 = 3530,

        /// <summary>
        /// Timestamp not yet valid
        /// </summary>
        NU3544 = 3544,

        /// <summary>
        /// Invalid timestamp in signature
        /// </summary>
        NU3550 = 3550,

        /// <summary>
        /// Timestamp unsupported algorithm
        /// </summary>
        NU3552 = 3552,

        /// <summary>
        /// Undefined Package Error.
        /// </summary>
        NU5000 = 5000,

        /// <summary>
        /// Error_WriteResolvedNuSpecOverwriteOriginal
        /// </summary>
        NU5001 = 5001,

        /// <summary>
        /// Error_InputFileNotSpecified
        /// </summary>
        NU5002 = 5002,

        /// <summary>
        /// Error_InvalidTargetFramework
        /// </summary>
        NU5003 = 5003,

        /// <summary>
        /// Error_PackageCommandNoFilesForLibPackage
        /// </summary>
        NU5004 = 5004,

        /// <summary>
        /// Error_PackageCommandNoFilesForSymbolsPackage
        /// </summary>
        NU5005 = 5005,

        /// <summary>
        /// Error_PackFailed
        /// </summary>
        NU5006 = 5006,

        /// <summary>
        /// Error_UnableToLocateBuildOutput
        /// </summary>
        NU5007 = 5007,

        /// <summary>
        /// ErrorManifestFileNotFound
        /// </summary>
        NU5008 = 5008,

        /// <summary>
        /// Undefined package warning
        /// </summary>
        NU5500 = 5500,

    }
}