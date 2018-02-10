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
        private static readonly string ToolsDirectory = PackagingConstants.Folders.Tools;

        public IEnumerable<PackLogMessage> Validate(PackageArchiveReader builder)
        {
            foreach (var file in builder.GetFiles())
            {
                if (!file.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!file.StartsWith(ToolsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssueForMisplacedScript(file);
                }
                else
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!name.Equals("install", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("uninstall", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("init", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreatePackageIssueForUnrecognizedScripts(file);
                    }
                }
            }
        }

        private static PackLogMessage CreatePackageIssueForMisplacedScript(string target)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.ScriptOutsideToolsWarning, target),
                NuGetLogCode.NU5110);
        }

        private static PackLogMessage CreatePackageIssueForUnrecognizedScripts(string target)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.UnrecognizedScriptWarning, target),
                NuGetLogCode.NU5111);
        }
    }
}