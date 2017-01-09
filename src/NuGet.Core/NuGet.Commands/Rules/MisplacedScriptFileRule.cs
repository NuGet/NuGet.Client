using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class MisplacedScriptFileRule : IPackageRule
    {
        private const string ScriptExtension = ".ps1";
        private const string ToolsDirectory = "tools";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            foreach (IPackageFile file in builder.Files)
            {
                string path = file.Path;
                if (!path.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!path.StartsWith(ToolsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssueForMisplacedScript(path);
                }
                else
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!name.Equals("install", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("uninstall", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("init", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreatePackageIssueForUnrecognizedScripts(path);
                    }
                }
            }
        }

        private static PackageIssue CreatePackageIssueForMisplacedScript(string target)
        {
            return new PackageIssue(
                AnalysisResources.ScriptOutsideToolsTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.ScriptOutsideToolsDescription, target),
                AnalysisResources.ScriptOutsideToolsSolution
            );
        }

        private static PackageIssue CreatePackageIssueForUnrecognizedScripts(string target)
        {
            return new PackageIssue(
                AnalysisResources.UnrecognizedScriptTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.UnrecognizedScriptDescription, target),
                AnalysisResources.UnrecognizedScriptSolution
            );
        }
    }
}