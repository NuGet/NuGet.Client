using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class MisplacedTransformFileRule : IPackageRule
    {
        private const string CodeTransformExtension = ".pp";
        private const string ConfigTransformExtension = ".transform";
        private const string ContentDirectory = "content";
        private const string ContentFilesDirectory = "contentFiles";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            foreach (IPackageFile file in builder.Files)
            {
                string path = file.Path;

                // if not a .transform file, ignore
                if (!path.EndsWith(CodeTransformExtension, StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(ConfigTransformExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // if not inside 'content' folder, warn
                if (!path.StartsWith(ContentDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith(ContentFilesDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssueForMisplacedContent(path);
                }
            }
        }

        private static PackageIssue CreatePackageIssueForMisplacedContent(string path)
        {
            return new PackageIssue(
                AnalysisResources.MisplacedTransformFileTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.MisplacedTransformFileDescription, path),
                AnalysisResources.MisplacedTransformFileSolution
            );
        }
    }
}