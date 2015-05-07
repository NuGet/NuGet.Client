// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTE;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class VSVersionHelper
    {
        internal static string GetSKU()
        {
            DTE dte = ServiceLocator.GetInstance<DTE>();
            string sku = dte.Edition;
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
    }
}
