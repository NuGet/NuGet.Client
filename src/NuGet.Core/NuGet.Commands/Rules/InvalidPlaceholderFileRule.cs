using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class InvalidPlaceholderFileRule : IPackageRule
    {
        private const string PlaceholderFile = "_._";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            foreach (IPackageFile file in builder.Files)
            {
                string path = file.Path;
                if (Path.GetFileName(path).Equals(PlaceholderFile, StringComparison.Ordinal))
                {
                    yield return CreatePackageIssueForPlaceholderFile(path);
                }
            }
        }

        private static PackageIssue CreatePackageIssueForPlaceholderFile(string target)
        {
            return new PackageIssue(
                AnalysisResources.PlaceholderFileInPackageTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.PlaceholderFileInPackageDescription, target),
                AnalysisResources.PlaceholderFileInPackageSolution
            );
        }
    }
}