using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class WinRTNameIsObsoleteRule : IPackageRule
    {
        private static string[] Prefixes = new string[]
            { "content\\winrt45\\", "lib\\winrt45\\", "tools\\winrt45\\", "content\\winrt\\", "lib\\winrt\\", "tools\\winrt\\" };

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            foreach (var file in builder.Files)
            {
                foreach (string prefix in Prefixes)
                {
                    if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateIssue(file);
                    }
                }
            }
        }

        private static PackageIssue CreateIssue(IPackageFile file)
        {
            return new PackageIssue(
                AnalysisResources.WinRTObsoleteTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.WinRTObsoleteDescription, file.Path),
                AnalysisResources.WinRTObsoleteSolution);
        }
    }
}