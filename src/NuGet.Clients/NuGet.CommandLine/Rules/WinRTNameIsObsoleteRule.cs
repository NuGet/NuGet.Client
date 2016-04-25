using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace NuGet.CommandLine.Rules
{
    internal class WinRTNameIsObsoleteRule : IPackageRule
    {
        private static string[] Prefixes = new string[]
            { "content\\winrt45\\", "lib\\winrt45\\", "tools\\winrt45\\", "content\\winrt\\", "lib\\winrt\\", "tools\\winrt\\" };

        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            IEnumerable<IPackageFile> files = new List<IPackageFile>();
            try
            {
                files = package.GetFiles();
            }
            catch (XmlException)
            {
            }

            foreach (var file in files)
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