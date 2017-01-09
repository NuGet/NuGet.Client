using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class InvalidFrameworkFolderRule : IPackageRule
    {
        private const string LibDirectory = "lib";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in builder.Files)
            {
                string path = file.Path;
                string[] parts = path.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 3 && parts[0].Equals(LibDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(path);
                }
            }

            return set.Where(s => !IsValidFrameworkName(s) && !IsValidCultureName(builder, s))
                      .Select(CreatePackageIssue);
        }

        private static bool IsValidFrameworkName(string path)
        {
            FrameworkName fx;
            try
            {
                string effectivePath;
                fx = FrameworkNameUtility.ParseFrameworkNameFromFilePath(path, out effectivePath);
            }
            catch (ArgumentException)
            {
                fx = null;
            }

            return fx != null;
        }

        private static bool IsValidCultureName(PackageBuilder builder, string name)
        {
            // starting from NuGet 1.8, we support localized packages, which
            // can have a culture folder under lib, e.g. lib\fr-FR\strings.resources.dll

            if (String.IsNullOrEmpty(builder.Language))
            {
                return false;
            }

            // the folder name is considered valid if it matches the package's Language property.
            return name.Equals(builder.Language, StringComparison.OrdinalIgnoreCase);
        }

        private PackageIssue CreatePackageIssue(string target)
        {
            return new PackageIssue(
                AnalysisResources.InvalidFrameworkTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.InvalidFrameworkDescription, target),
                AnalysisResources.InvalidFrameworkSolution
            );
        }
    }
}