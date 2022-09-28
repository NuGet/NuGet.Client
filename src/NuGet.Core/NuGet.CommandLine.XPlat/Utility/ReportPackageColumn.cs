// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal enum ReportPackageColumn
    {
        [Description("Top-level Package")]
        TopLevelPackage,
        [Description("")]
        EmptyColumn,
        [Description("Requested")]
        Requested,
        [Description("Resolved")]
        Resolved,
        [Description("Transitive Package")]
        TransitivePackage,
        [Description("Latest")]
        Latest,
        [Description("Deprecated")]
        Deprecated,
        [Description("Alternate Package")]
        AlternatePackage,
        [Description("Severity")]
        VulnerabilitySeverity,
        [Description("Advisoryurl")]
        VulnerabilityAdvisoryurl
    }
}
