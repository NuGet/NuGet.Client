using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace NuGet.CommandLine.Rules
{
    internal class MisplacedTransformFileRule : IPackageRule
    {
        private const string CodeTransformExtension = ".pp";
        private const string ConfigTransformExtension = ".transform";

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
                string path = file.Path;

                // if not a .transform file, ignore
                if (!path.EndsWith(CodeTransformExtension, StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(ConfigTransformExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // if not inside 'content' folder, warn
                if (!path.StartsWith(Constants.ContentDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith(Constants.ContentFilesDirectory + Path.DirectorySeparatorChar,
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