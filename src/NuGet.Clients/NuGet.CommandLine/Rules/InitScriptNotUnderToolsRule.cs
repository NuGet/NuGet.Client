using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace NuGet.CommandLine.Rules
{
    internal class InitScriptNotUnderToolsRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            IEnumerable<IPackageFile> files = new List<IPackageFile>();
            try
            {
                files = package.GetToolFiles();
            }
            catch (XmlException)
            {
            }

            foreach (var file in files)
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