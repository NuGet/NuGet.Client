// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class MisplacedScriptFileRule : IPackageRule
    {
        private const string ScriptExtension = ".ps1";
        private const string ToolsDirectory = "tools";

        public IEnumerable<PackageIssueLogMessage> Validate(PackageBuilder builder)
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

        private static PackageIssueLogMessage CreatePackageIssueForMisplacedScript(string target)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.ScriptOutsideToolsWarning, target),
                NuGetLogCode.NU5110,
                WarningLevel.Default,
                LogLevel.Warning);
        }

        private static PackageIssueLogMessage CreatePackageIssueForUnrecognizedScripts(string target)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.UnrecognizedScriptWarning, target),
                NuGetLogCode.NU5111,
                WarningLevel.Default,
                LogLevel.Warning);
        }
    }
}