// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class MisplacedTransformFileRule : IPackageRule
    {
        private const string CodeTransformExtension = ".pp";
        private const string ConfigTransformExtension = ".transform";
        private const string ContentDirectory = "content";
        private const string ContentFilesDirectory = "contentFiles";

        public IEnumerable<PackageIssueLogMessage> Validate(PackageBuilder builder)
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

        private static PackageIssueLogMessage CreatePackageIssueForMisplacedContent(string path)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.MisplacedTransformFileWarning, path),
                NuGetLogCode.NU5108,
                WarningLevel.Default,
                LogLevel.Warning);
        }
    }
}