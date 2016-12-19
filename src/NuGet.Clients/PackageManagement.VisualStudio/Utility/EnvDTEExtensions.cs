// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class EnvDTEExtensions
    {
        public static string GetSKU(this DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sku = dte.Edition;
            if (sku.Equals("Ultimate", StringComparison.OrdinalIgnoreCase)
                ||
                sku.Equals("Premium", StringComparison.OrdinalIgnoreCase)
                ||
                sku.Equals("Professional", StringComparison.OrdinalIgnoreCase))
            {
                sku = "Pro";
            }

            return sku;
        }

        public static string GetFullVsVersionString(this DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // On Dev14, dte.Edition just returns SKU, such as "Enterprise"
            // Add "VS" to the string so that in user agent header, it will be "VS Enterprise/14.0".
            var edition = dte.Edition;
            if (!edition.StartsWith("VS", StringComparison.OrdinalIgnoreCase))
            {
                edition = "VS " + edition;
            }

            return edition + "/" + dte.Version;
        }
    }
}