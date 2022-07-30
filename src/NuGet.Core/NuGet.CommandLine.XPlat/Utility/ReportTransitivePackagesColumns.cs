// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal enum ReportTransitivePackagesColumns
    {
        [Description("Transitive Package")]
        TransitivePackage,
        [Description("")]
        EmptyColumn,
        [Description("Resolved")]
        Resolved
    }
}
