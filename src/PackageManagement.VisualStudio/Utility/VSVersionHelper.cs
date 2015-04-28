using EnvDTE;
using System;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class VSVersionHelper
    {
        internal static string GetSKU()
        {
            DTE dte = ServiceLocator.GetInstance<DTE>();
            string sku = dte.Edition;
            if (sku.Equals("Ultimate", StringComparison.OrdinalIgnoreCase) ||
                sku.Equals("Premium", StringComparison.OrdinalIgnoreCase) ||
                sku.Equals("Professional", StringComparison.OrdinalIgnoreCase))
            {
                sku = "Pro";
            }

            return sku;
        }
    }
}
