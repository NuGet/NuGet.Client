// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class InvalidFrameworkFolderRule : IPackageRule
    {
        private const string LibDirectory = "lib";

        public string MessageFormat { get; }

        public InvalidFrameworkFolderRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in builder.GetFiles().Select(t => PathUtility.GetPathWithDirectorySeparator(t)))
            {
                var parts = file.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 3 && parts[0].Equals(LibDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(file);
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

        private static bool IsValidCultureName(PackageArchiveReader builder, string name)
        {
            // starting from NuGet 1.8, we support localized packages, which
            // can have a culture folder under lib, e.g. lib\fr-FR\strings.resources.dll
            var nuspecReader = builder.NuspecReader;
            if (string.IsNullOrEmpty(nuspecReader.GetLanguage()))
            {
                return false;
            }

            // the folder name is considered valid if it matches the package's Language property.
            return name.Equals(nuspecReader.GetLanguage(), StringComparison.OrdinalIgnoreCase);
        }

        private PackagingLogMessage CreatePackageIssue(string target)
        {
            return PackagingLogMessage.CreateWarning(
                string.Format(CultureInfo.CurrentCulture, MessageFormat, target),
                NuGetLogCode.NU5103);
        }
    }
}