using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class InitScriptNotUnderToolsRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            foreach (var file in builder.Files)
            {
                string name = Path.GetFileName(file.Path);
                if (file.TargetFramework != null && name.Equals("init.ps1", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssue(file);
                }
            }
        }

        private static PackageIssue CreatePackageIssue(IPackageFile file)
        {
            return new PackageIssue(
                AnalysisResources.MisplacedInitScriptTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.MisplacedInitScriptDescription, file.Path),
                AnalysisResources.MisplacedInitScriptSolution);
        }
    }
}