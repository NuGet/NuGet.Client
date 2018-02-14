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

        public IEnumerable<PackLogMessage> Validate(PackageArchiveReader builder)
        {
            foreach (var file in builder.GetFiles())
            {
                // if not a .transform file, ignore
                if (!file.EndsWith(CodeTransformExtension, StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(ConfigTransformExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // if not inside 'content' folder, warn
                if (!file.StartsWith(ContentDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    && !file.StartsWith(ContentFilesDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssueForMisplacedContent(file);
                }
            }
        }

        private static PackLogMessage CreatePackageIssueForMisplacedContent(string path)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.MisplacedTransformFileWarning, path),
                NuGetLogCode.NU5108);
        }
    }
}